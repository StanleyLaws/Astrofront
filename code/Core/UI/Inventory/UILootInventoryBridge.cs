using Sandbox;
using System;
using System.Linq;

namespace Astrofront;

/// <summary>
/// Pont autoritaire (host) entre l'UI loot (GroundItemsPanel) et l'inventaire.
/// Anti-dup: le host est la source de vérité (scan des pickups + retrait réel).
/// </summary>
public static class UiLootInventoryBridge
{
	/// <summary>
	/// UI -> Host : prendre du loot (ItemId) vers un slot d'inventaire.
	/// amountWanted = ce que l'utilisateur essaie de placer (1 ou stack complet).
	/// </summary>
	[Rpc.Host]
	public static void RequestLootToInventory( string itemId, int amountWanted, int inventorySlot )
	{
		var caller = Rpc.Caller ?? Connection.Local;
		if ( caller == null ) return;

		if ( string.IsNullOrEmpty( itemId ) || amountWanted <= 0 )
			return;

		var scene = Game.ActiveScene;
		if ( scene == null ) return;

		// PlayerState du caller
		var ps = scene.GetAllComponents<PlayerState>()
			.FirstOrDefault( p => p != null && p.Network != null && p.Network.Owner == caller );

		if ( ps == null ) return;

		// Inventaire
		var inv = ps.GameObject.Components.Get<InventoryComponent>( FindMode.EverythingInSelfAndDescendants );
		if ( inv == null ) return;

		// Clamp slot (hands interdit)
		inventorySlot = inventorySlot.Clamp( 0, inv.SlotCount - 1 );
		if ( inventorySlot == 0 ) return;

		int remaining = amountWanted;
		int movedTotal = 0;

		// Scan pickups proches (côté host)
		var pickups = scene.GetAllComponents<GroundItemPickup>()
			.Where( p => p != null
						&& p.Amount > 0
						&& p.ItemId == itemId
						&& IsNearPlayer( p, ps, GroundItemsService.ScanRadius ) )
			.OrderBy( p => (p.Transform.World.Position - ps.Transform.World.Position).LengthSquared )
			.ToList();

		foreach ( var pickup in pickups )
		{
			if ( remaining <= 0 ) break;

			int wantTake = Math.Min( remaining, pickup.Amount );
			if ( wantTake <= 0 ) continue;

			// Tente de placer (host clamp stack/capacité)
			int placed = inv.PlaceIntoSlotHost( inventorySlot, itemId, wantTake );

			if ( placed <= 0 )
			{
				// plus de place => stop
				break;
			}

			// Consomme le pickup
			pickup.Amount -= placed;
			movedTotal += placed;
			remaining -= placed;

			if ( pickup.Amount <= 0 )
				pickup.GameObject.Destroy();
		}

		// Retour uniquement au caller (sans Rpc.To)
		using ( Rpc.FilterInclude( c => c == caller ) )
		{
			LootToInventoryResult( itemId, movedTotal, amountWanted, inventorySlot );
		}

		if ( movedTotal > 0 )
			Log.Info( $"[Loot->Inv] +{movedTotal}/{amountWanted} {itemId} to slot {inventorySlot} ({caller.DisplayName})" );
	}

	/// <summary>
	/// UI -> Host : spawn au sol depuis l'inventaire (quand tu places dans loot panel).
	/// IMPORTANT : "réalisme" => 1 pickup = 1 item. Donc on spawn N GameObjects avec Amount = 1.
	/// </summary>
	[Rpc.Host]
	public static void RequestSpawnToWorld( string itemId, int amount )
	{
		var caller = Rpc.Caller ?? Connection.Local;
		if ( caller == null ) return;

		if ( string.IsNullOrEmpty( itemId ) || amount <= 0 )
			return;

		var scene = Game.ActiveScene;
		if ( scene == null ) return;

		var ps = scene.GetAllComponents<PlayerState>()
			.FirstOrDefault( p => p != null && p.Network != null && p.Network.Owner == caller );

		if ( ps == null ) return;

		SpawnPickupsInFrontOf( scene, ps, itemId, amount );

		// retour client (juste log)
		using ( Rpc.FilterInclude( c => c == caller ) )
		{
			SpawnToWorldResult( itemId, amount );
		}

		Log.Info( $"[Inv->World] spawn +{amount} {itemId} ({caller.DisplayName})" );
	}

	// ========= Client feedback =========

	[Rpc.Broadcast]
	private static void LootToInventoryResult( string itemId, int moved, int wanted, int slot )
	{
		Log.Info( $"[Loot->Inv] +{moved}/{wanted} {itemId} -> slot {slot}" );

		// ✅ Si on avait cet item en main depuis le loot, on retire ce qui a vraiment été déplacé
		if ( moved > 0
			&& UiDragContext.HasItem
			&& UiDragContext.SourceKind == UiDragSourceKind.LootPanel
			&& UiDragContext.HeldItemId == itemId )
		{
			UiDragContext.TakeFromHand( moved ); // Clear() si ça tombe à 0
		}
	}


	[Rpc.Broadcast]
	private static void SpawnToWorldResult( string itemId, int amount )
	{
		Log.Info( $"[Inv->World] spawn +{amount} {itemId}" );
	}

	// ========= Helpers =========

	private static bool IsNearPlayer( GroundItemPickup pickup, PlayerState ps, float radius )
	{
		float r2 = radius * radius;
		return (pickup.Transform.World.Position - ps.Transform.World.Position).LengthSquared <= r2;
	}

	/// <summary>
	/// Spawn "réaliste" : 1 pickup = 1 item (Amount = 1).
	/// Ajoute un petit jitter pour éviter la superposition parfaite (sinon tu crois qu'il n'y en a qu'un).
	/// </summary>
	private static void SpawnPickupsInFrontOf( Scene scene, PlayerState ps, string itemId, int amount )
{
	if ( scene == null || ps == null ) return;
	if ( string.IsNullOrEmpty( itemId ) || amount <= 0 ) return;

	var tr = ps.Transform.World;

	// point de base devant le joueur
	var basePos = tr.Position + tr.Rotation.Forward * 48f + Vector3.Up * 8f;

	// petit étalement en arc pour éviter que tout soit au même point
	var right = tr.Rotation.Right;
	var forward = tr.Rotation.Forward;

	for ( int i = 0; i < amount; i++ )
	{
		var go = scene.CreateObject();
		go.Name = $"ground_{itemId}"; // debug only

		// offset léger
		float t = (amount <= 1) ? 0f : (i / (float)(amount - 1)) - 0.5f; // -0.5..+0.5
		var offset = right * (t * 10f) + forward * (MathF.Abs(t) * 4f);

		go.Transform.World = new Transform( basePos + offset, Rotation.Identity );

		var pickup = go.Components.Create<GroundItemPickup>();
		pickup.ItemId = itemId;
		pickup.Amount = 1; // ✅ 1 par pickup

		go.NetworkSpawn();
	}
}

}
