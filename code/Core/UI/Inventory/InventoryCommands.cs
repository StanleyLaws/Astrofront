using Sandbox;
using System.Linq;

namespace Astrofront;

public static class InventoryCommands
{
	// Trouver l'inventaire du joueur local (client) OU du caller (serveur)
	private static InventoryComponent FindInventoryFor( Connection conn )
	{
		var scene = Game.ActiveScene;
		if ( scene == null || conn == null ) return null;

		var ps = scene.GetAllComponents<PlayerState>()
			.FirstOrDefault( p => p != null && p.Network != null && p.Network.Owner == conn );

		if ( ps == null ) return null;

		return ps.GameObject?.Components.Get<InventoryComponent>( FindMode.InSelf | FindMode.InChildren );
	}
	
	
	private static void SpawnPickupInFrontOf( Connection conn, string itemId, int amount )
{
	var scene = Game.ActiveScene;
	if ( scene == null || conn == null ) return;

	var ps = scene.GetAllComponents<PlayerState>()
		.FirstOrDefault( p => p != null && p.Network != null && p.Network.Owner == conn );

	if ( ps == null ) return;

	var tr = ps.Transform.World;
	var pos = tr.Position + tr.Rotation.Forward * 48f + Vector3.Up * 8f;

	var go = scene.CreateObject();
	go.Name = $"ground_{itemId}";
	go.Transform.World = new Transform( pos, Rotation.Identity );

	var pickup = go.Components.Create<GroundItemPickup>();
	pickup.ItemId = itemId;
	pickup.Amount = amount;

	go.NetworkSpawn();
}


	/// <summary>
	/// Donne un item à ton inventaire (serveur autoritaire).
	/// Usage: inv_give core.test.item 10
	/// </summary>
	[ConCmd( "inv_give" )]
	public static void Give( string itemId, int amount = 1 )
	{
		if ( string.IsNullOrEmpty( itemId ) || amount <= 0 )
		{
			Log.Info( "Usage: inv_give <itemId> <amount>" );
			return;
		}

		// La commande est appelée côté client: on passe par RPC host
		GiveHost( itemId, amount );
	}

	[ConCmd( "inv_print" )]
	public static void Print()
	{
		PrintHost();
	}

	[ConCmd( "inv_clear" )]
	public static void Clear()
	{
		ClearHost();
	}

	// ---------------- Host RPC-like via ConCmd (s&box exécute souvent côté client)
	// On passe via [Rpc.Host] pour forcer le serveur à appliquer.

	[Rpc.Host]
	private static void GiveHost( string itemId, int amount )
	{
		var caller = Rpc.Caller ?? Connection.Local;
		if ( caller == null ) return;

		var inv = FindInventoryFor( caller );
		if ( inv == null )
		{
			Log.Warning( "[inv_give] Inventory not found on caller." );
			return;
		}

		var def = ItemRegistry.Get( itemId );
if ( def == null )
{
	Log.Warning( $"[inv_give] Unknown ItemId: {itemId}" );
	return;
}


		var added = inv.AddHost( itemId, amount );
		Log.Info( $"[inv_give] +{added}/{amount} {itemId} (to {caller.DisplayName})" );
	}

	[Rpc.Host]
	private static void PrintHost()
	{
		var caller = Rpc.Caller ?? Connection.Local;
		if ( caller == null ) return;

		var inv = FindInventoryFor( caller );
		if ( inv == null )
		{
			Log.Warning( "[inv_print] Inventory not found." );
			return;
		}

		var slots = inv.GetSlotsSnapshot();
		Log.Info( $"=== INVENTORY {caller.DisplayName} ===" );
		for ( int i = 0; i < slots.Count; i++ )
		{
			var (id, amt) = slots[i];
			if ( i == 0 )
			{
				Log.Info( $"[{i}] HANDS" );
				continue;
			}

			if ( amt <= 0 || string.IsNullOrEmpty( id ) )
				Log.Info( $"[{i}] (empty)" );
			else
				Log.Info( $"[{i}] {id} x{amt}" );
		}
		Log.Info( "=========================" );
	}

	[Rpc.Host]
	private static void ClearHost()
	{
		var caller = Rpc.Caller ?? Connection.Local;
		if ( caller == null ) return;

		var inv = FindInventoryFor( caller );
		if ( inv == null )
		{
			Log.Warning( "[inv_clear] Inventory not found." );
			return;
		}

		// vider slots 1..end via TakeFromSlotHost
		for ( int i = 1; i < inv.SlotCount; i++ )
		{
			// on retire un grand nombre pour forcer à zéro
			inv.TakeFromSlotHost( i, 9999999 );
		}

		Log.Info( $"[inv_clear] cleared for {caller.DisplayName}" );
	}
	
	[ConCmd( "inv_drop" )]
public static void Drop( int slot, int amount = 1 )
{
	if ( amount <= 0 ) amount = 1;
	DropHost( slot, amount );
}

[Rpc.Host]
private static void DropHost( int slot, int amount )
{
	var caller = Rpc.Caller ?? Connection.Local;
	if ( caller == null ) return;

	var inv = FindInventoryFor( caller );
	if ( inv == null ) return;

	// Slot 0 = hands interdit
	if ( slot <= 0 || slot >= inv.SlotCount )
	{
		Log.Info( "Usage: inv_drop <slot 1..4> <amount>" );
		return;
	}

	// Lire le contenu (il faut un snapshot ou getters)
	var slots = inv.GetSlotsSnapshot();
	var (id, amt) = slots[slot];

	if ( string.IsNullOrEmpty( id ) || amt <= 0 )
	{
		Log.Info( $"[inv_drop] slot {slot} empty" );
		return;
	}

	int take = amount;
	if ( take > amt ) take = amt;

	var taken = inv.TakeFromSlotHost( slot, take );
	if ( taken <= 0 ) return;

	SpawnPickupInFrontOf( caller, id, taken );
	Log.Info( $"[inv_drop] dropped {taken} {id} from slot {slot}" );
}


	[ConCmd( "inv_pickup_near" )]
public static void PickupNear()
{
	PickupNearHost();
}

[Rpc.Host]
private static void PickupNearHost()
{
	var caller = Rpc.Caller ?? Connection.Local;
	if ( caller == null ) return;

	var scene = Game.ActiveScene;
	if ( scene == null ) return;

	var ps = scene.GetAllComponents<PlayerState>()
		.FirstOrDefault( p => p != null && p.Network != null && p.Network.Owner == caller );

	if ( ps == null ) return;

	var pos = ps.Transform.World.Position;
	float r = 160f;
	float r2 = r * r;

	var pickup = scene.GetAllComponents<GroundItemPickup>()
		.Where( p => p != null && p.Amount > 0 && (p.Transform.World.Position - pos).LengthSquared <= r2 )
		.OrderBy( p => (p.Transform.World.Position - pos).LengthSquared )
		.FirstOrDefault();

	if ( pickup == null )
	{
		Log.Info( "[inv_pickup_near] no pickup nearby" );
		return;
	}

	pickup.TryPickupHost();
}



	
	
	
}
