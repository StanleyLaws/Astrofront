using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astrofront;

public sealed class InventoryComponent : Component
{
	[Property] public int SlotCount { get; set; } = 5;
	[Property] public int SelectedIndex { get; private set; } = 0;

	[Property, Title( "Inventory Capacity (space units)" )]
	public int CapacitySpace { get; set; } = 10;

	// Snapshot local (vérité côté host, miroir côté owner)
	private string[] _itemIds;
	private int[] _amounts;

	public event Action SlotsChanged;
	public event Action<int> SelectionChanged;

	protected override void OnStart()
	{
		if ( SlotCount < 1 ) SlotCount = 1;

		_itemIds = new string[SlotCount];
		_amounts = new int[SlotCount];

		// Slot 0 = HANDS réservé
		_itemIds[0] = string.Empty;
		_amounts[0] = 0;

		SelectedIndex = SelectedIndex.Clamp( 0, SlotCount - 1 );

		if ( Networking.IsHost )
			PushSnapshotToOwner();

		SelectionChanged?.Invoke( SelectedIndex );
		SlotsChanged?.Invoke();
	}

	// ---------------- Selection ----------------

	public void SetSelected( int index )
	{
		index = index.Clamp( 0, SlotCount - 1 );
		if ( index == SelectedIndex ) return;

		SelectedIndex = index;
		SelectionChanged?.Invoke( SelectedIndex );
	}

	// ---------------- Snapshot (UI) ----------------

	public IReadOnlyList<(string ItemId, int Amount)> GetSlotsSnapshot()
	{
		var list = new List<(string, int)>( SlotCount );
		for ( int i = 0; i < SlotCount; i++ )
			list.Add( (_itemIds[i], _amounts[i]) );
		return list;
	}

	// ---------------- Capacity (client-safe) ----------------

	public int UsedSpace()
	{
		int used = 0;

		// slot 0 = hands ignoré
		for ( int i = 1; i < SlotCount; i++ )
		{
			int amt = _amounts[i];
			if ( amt <= 0 ) continue;

			var id = _itemIds[i];
			if ( string.IsNullOrEmpty( id ) ) continue;

			int cost = ItemRegistry.GetSpaceCost( id );
			used += amt * cost;
		}

		return used;
	}

	public int FreeSpace() => Math.Max( 0, CapacitySpace - UsedSpace() );

	// ---------------- Host operations (truth) ----------------

	/// <summary>
	/// Ajout générique : merge puis slots vides. Retourne la quantité acceptée.
	/// </summary>
	public int AddHost( string itemId, int amount )
	{
		if ( !Networking.IsHost ) return 0;
		if ( string.IsNullOrEmpty( itemId ) || amount <= 0 ) return 0;

		int cost = ItemRegistry.GetSpaceCost( itemId );
		int roomUnits = FreeSpace() / Math.Max( 1, cost );
		if ( roomUnits <= 0 ) return 0;

		int accepted = Math.Min( amount, roomUnits );
		int remaining = accepted;

		int maxStack = ItemRegistry.GetMaxStack( itemId );

		// 1) merge stacks existants
		for ( int i = 1; i < SlotCount && remaining > 0; i++ )
		{
			if ( _amounts[i] <= 0 ) continue;
			if ( _itemIds[i] != itemId ) continue;

			int canAdd = Math.Max( 0, maxStack - _amounts[i] );
			int take = Math.Min( remaining, canAdd );
			if ( take <= 0 ) continue;

			_amounts[i] += take;
			remaining -= take;
		}

		// 2) slots vides
		for ( int i = 1; i < SlotCount && remaining > 0; i++ )
		{
			if ( _amounts[i] > 0 ) continue;

			int take = Math.Min( remaining, maxStack );
			_itemIds[i] = itemId;
			_amounts[i] = take;
			remaining -= take;
		}

		int actually = accepted - remaining;
		if ( actually > 0 )
		{
			PushSnapshotToOwner();
			Log.Info( $"[Inventory] +{actually} {itemId}" );
		}

		return actually;
	}

	public void SwapHost( int a, int b )
	{
		if ( !Networking.IsHost ) return;
		if ( a == 0 || b == 0 ) return;
		if ( a < 0 || b < 0 || a >= SlotCount || b >= SlotCount ) return;
		if ( a == b ) return;

		(_itemIds[a], _itemIds[b]) = (_itemIds[b], _itemIds[a]);
		(_amounts[a], _amounts[b]) = (_amounts[b], _amounts[a]);

		PushSnapshotToOwner();
	}

	public int TakeFromSlotHost( int index, int amount )
	{
		if ( !Networking.IsHost ) return 0;
		if ( index == 0 ) return 0;
		if ( index < 0 || index >= SlotCount ) return 0;
		if ( amount <= 0 ) return 0;

		int take = Math.Min( amount, _amounts[index] );
		if ( take <= 0 ) return 0;

		_amounts[index] -= take;

		if ( _amounts[index] <= 0 )
		{
			_amounts[index] = 0;
			_itemIds[index] = string.Empty;
		}

		PushSnapshotToOwner();
		return take;
	}

	public int PlaceIntoSlotHost( int index, string itemId, int amount )
	{
		if ( !Networking.IsHost ) return 0;
		if ( index == 0 ) return 0;
		if ( index < 0 || index >= SlotCount ) return 0;
		if ( string.IsNullOrEmpty( itemId ) || amount <= 0 ) return 0;

		int cost = Math.Max( 1, ItemRegistry.GetSpaceCost( itemId ) );
		int maxStack = ItemRegistry.GetMaxStack( itemId );

		// slot vide
		if ( _amounts[index] <= 0 )
		{
			int room = FreeSpace() / cost;
			int take = Math.Min( amount, Math.Min( room, maxStack ) );
			if ( take <= 0 ) return 0;

			_itemIds[index] = itemId;
			_amounts[index] = take;

			PushSnapshotToOwner();
			return take;
		}

		// slot plein : doit être même item
		if ( _itemIds[index] != itemId ) return 0;

		int canAddByStack = Math.Max( 0, maxStack - _amounts[index] );
		if ( canAddByStack <= 0 ) return 0;

		int room2 = FreeSpace() / cost;
		int take2 = Math.Min( amount, Math.Min( canAddByStack, room2 ) );
		if ( take2 <= 0 ) return 0;

		_amounts[index] += take2;
		PushSnapshotToOwner();
		return take2;
	}

	// ---------------- Network sync ----------------

	private void PushSnapshotToOwner()
	{
		if ( !Networking.IsHost ) return;

		var owner = Network?.Owner;
		if ( owner == null ) return;

		var ids = new string[SlotCount];
		var amts = new int[SlotCount];

		for ( int i = 0; i < SlotCount; i++ )
		{
			ids[i] = _itemIds[i];
			amts[i] = _amounts[i];
		}

		PushSnapshotOwner( ids, amts );
	}

	[Rpc.Owner]
	private void PushSnapshotOwner( string[] itemIds, int[] amounts )
	{
		if ( itemIds == null || amounts == null ) return;

		_itemIds = itemIds;
		_amounts = amounts;

		SlotsChanged?.Invoke();
	}

	// ---------------- Owner-authorized RPCs ----------------

	private bool IsCallerOwner()
	{
		var caller = Rpc.Caller ?? Connection.Local;
		if ( caller == null ) return false;
		return Network?.Owner == caller;
	}

	[Rpc.Host]
	public void RequestSwapHost( int a, int b )
	{
		if ( !IsCallerOwner() ) return;
		SwapHost( a, b );
	}

	[Rpc.Host]
	public void RequestTakeHost( int slot, int amount )
	{
		if ( !IsCallerOwner() ) return;
		TakeFromSlotHost( slot, amount );
	}

	[Rpc.Host]
	public void RequestPlaceHost( int slot, string itemId, int amount )
	{
		if ( !IsCallerOwner() ) return;
		PlaceIntoSlotHost( slot, itemId, amount );
	}

	/// <summary>
	/// ✅ NOUVEAU : transaction atomique inventaire -> inventaire basée sur une "main UI-only".
	/// Retire depuis fromSlot puis place dans toSlot (slot vide ou même item uniquement).
	/// Le callback côté client décrémente UiDragContext uniquement du moved réel.
	/// </summary>
	[Rpc.Host]
	public void RequestMoveHeldHost( int fromSlot, int toSlot, int amountWanted )
	{
		if ( !IsCallerOwner() ) return;

		if ( fromSlot == 0 || toSlot == 0 ) { SendMoveHeldResultToCaller( "", 0, fromSlot, toSlot ); return; }
		if ( fromSlot < 0 || toSlot < 0 || fromSlot >= SlotCount || toSlot >= SlotCount ) { SendMoveHeldResultToCaller( "", 0, fromSlot, toSlot ); return; }
		if ( fromSlot == toSlot ) { SendMoveHeldResultToCaller( "", 0, fromSlot, toSlot ); return; }
		if ( amountWanted <= 0 ) { SendMoveHeldResultToCaller( "", 0, fromSlot, toSlot ); return; }

		var srcId = _itemIds[fromSlot];
		var srcAmt = _amounts[fromSlot];
		if ( string.IsNullOrEmpty( srcId ) || srcAmt <= 0 )
		{
			SendMoveHeldResultToCaller( "", 0, fromSlot, toSlot );
			return;
		}

		int want = Math.Min( amountWanted, srcAmt );

		// Destination : autorise uniquement vide ou même item (pas de swap ici)
		if ( _amounts[toSlot] > 0 && _itemIds[toSlot] != srcId )
		{
			SendMoveHeldResultToCaller( srcId, 0, fromSlot, toSlot );
			return;
		}

		// 1) retire réellement de la source (libère capacité si besoin)
		int taken = TakeFromSlotHost( fromSlot, want );
		if ( taken <= 0 )
		{
			SendMoveHeldResultToCaller( srcId, 0, fromSlot, toSlot );
			return;
		}

		// 2) place dans la destination (host clamp stack + capacité)
		int placed = PlaceIntoSlotHost( toSlot, srcId, taken );

		// 3) remet le reste dans la source (jamais delete)
		int remaining = taken - placed;
		if ( remaining > 0 )
		{
			PlaceIntoSlotHost( fromSlot, srcId, remaining );
		}

		SendMoveHeldResultToCaller( srcId, placed, fromSlot, toSlot );
	}

	private void SendMoveHeldResultToCaller( string itemId, int moved, int fromSlot, int toSlot )
	{
		var caller = Rpc.Caller ?? Connection.Local;
		if ( caller == null ) return;

		using ( Rpc.FilterInclude( c => c == caller ) )
		{
			MoveHeldResult( itemId ?? "", moved, fromSlot, toSlot );
		}
	}

	[Rpc.Broadcast]
	private void MoveHeldResult( string itemId, int moved, int fromSlot, int toSlot )
	{
		if ( moved <= 0 ) return;

		// Si on tient bien cet item depuis ce slot inventaire, on décrémente la main fantôme
		if ( UiDragContext.HasItem
			&& UiDragContext.SourceKind == UiDragSourceKind.Inventory
			&& UiDragContext.SourceIndex == fromSlot
			&& string.Equals( UiDragContext.HeldItemId, itemId, StringComparison.OrdinalIgnoreCase ) )
		{
			UiDragContext.TakeFromHand( moved );
		}
	}

	[Rpc.Host]
	public void RequestDropHost( int slot, int amount )
	{
		if ( !IsCallerOwner() ) return;
		if ( slot == 0 ) return;
		if ( slot < 0 || slot >= SlotCount ) return;
		if ( amount <= 0 ) return;

		var itemId = _itemIds[slot];
		if ( string.IsNullOrEmpty( itemId ) ) return;

		int taken = TakeFromSlotHost( slot, amount );
		if ( taken <= 0 ) return;

		SpawnPickupsInFrontOf( Rpc.Caller ?? Connection.Local, itemId, taken );
	}

	[Rpc.Host]
	public void RequestRefundHeldHost( string itemId, int amount, int preferredSlot )
	{
		if ( !IsCallerOwner() ) return;
		if ( string.IsNullOrEmpty( itemId ) || amount <= 0 ) return;

		preferredSlot = preferredSlot.Clamp( 0, SlotCount - 1 );
		if ( preferredSlot == 0 ) preferredSlot = 1;

		int remaining = amount;

		// 1) slot préféré (souvent l'origine)
		remaining -= PlaceIntoSlotHost( preferredSlot, itemId, remaining );

		// 2) merge dans stacks existants
		for ( int i = 1; i < SlotCount && remaining > 0; i++ )
		{
			if ( i == preferredSlot ) continue;
			if ( _amounts[i] > 0 && _itemIds[i] == itemId )
				remaining -= PlaceIntoSlotHost( i, itemId, remaining );
		}

		// 3) slots vides
		for ( int i = 1; i < SlotCount && remaining > 0; i++ )
		{
			if ( _amounts[i] <= 0 )
				remaining -= PlaceIntoSlotHost( i, itemId, remaining );
		}

		// 4) si vraiment pas possible : on drop le reste (pour ne jamais "delete")
		if ( remaining > 0 )
		{
			SpawnPickupsInFrontOf( Rpc.Caller ?? Connection.Local, itemId, remaining );
		}
	}
	
	[Rpc.Host]
public void RequestDropHeldToWorldHost( int fromSlot, string itemId, int amountWanted )
{
	if ( !IsCallerOwner() ) return;

	if ( fromSlot == 0 ) { SendDropHeldResultToCaller( itemId, 0, fromSlot ); return; }
	if ( fromSlot < 0 || fromSlot >= SlotCount ) { SendDropHeldResultToCaller( itemId, 0, fromSlot ); return; }
	if ( string.IsNullOrEmpty( itemId ) || amountWanted <= 0 ) { SendDropHeldResultToCaller( itemId, 0, fromSlot ); return; }

	// Source doit toujours contenir cet item
	var srcId = _itemIds[fromSlot];
	var srcAmt = _amounts[fromSlot];

	if ( string.IsNullOrEmpty( srcId ) || srcAmt <= 0 )
	{
		SendDropHeldResultToCaller( itemId, 0, fromSlot );
		return;
	}

	if ( !string.Equals( srcId, itemId, StringComparison.OrdinalIgnoreCase ) )
	{
		SendDropHeldResultToCaller( itemId, 0, fromSlot );
		return;
	}

	int want = Math.Min( amountWanted, srcAmt );

	// Retire réellement
	int taken = TakeFromSlotHost( fromSlot, want );
	if ( taken <= 0 )
	{
		SendDropHeldResultToCaller( itemId, 0, fromSlot );
		return;
	}

	// Spawn au monde
	SpawnPickupsInFrontOf( Rpc.Caller ?? Connection.Local, itemId, taken );

	// Feedback caller => décrémente main fantôme
	SendDropHeldResultToCaller( itemId, taken, fromSlot );
}

private void SendDropHeldResultToCaller( string itemId, int moved, int fromSlot )
{
	var caller = Rpc.Caller ?? Connection.Local;
	if ( caller == null ) return;

	using ( Rpc.FilterInclude( c => c == caller ) )
	{
		DropHeldToWorldResult( itemId ?? "", moved, fromSlot );
	}
}

[Rpc.Broadcast]
private void DropHeldToWorldResult( string itemId, int moved, int fromSlot )
{
	if ( moved <= 0 ) return;

	if ( UiDragContext.HasItem
		&& UiDragContext.SourceKind == UiDragSourceKind.Inventory
		&& UiDragContext.SourceIndex == fromSlot
		&& string.Equals( UiDragContext.HeldItemId, itemId, StringComparison.OrdinalIgnoreCase ) )
	{
		UiDragContext.TakeFromHand( moved );
	}
}

	
	

	// ---------------- Spawn pickup ----------------

	private void SpawnPickupsInFrontOf( Connection conn, string itemId, int amount )
	{
		if ( !Networking.IsHost ) return;

		var ps = Scene?.GetAllComponents<PlayerState>()
			?.FirstOrDefault( p => p != null && p.Network != null && p.Network.Owner == conn );

		if ( ps == null ) return;

		var tr = ps.Transform.World;
		var basePos = tr.Position + tr.Rotation.Forward * 48f + Vector3.Up * 8f;

		// ✅ MODE 1 : drop "chaque item" (1 pickup = 1 unité)
		// Si tu préfères dropper par stacks, remplace 1 par ItemRegistry.GetMaxStack(itemId)
		int remaining = amount;

		while ( remaining > 0 )
		{
			int dropNow = 1;

			var go = Scene.CreateObject();
			go.Name = $"ground_{itemId}";

			// Petit spread pour éviter la superposition parfaite
			var jitter = new Vector3(
				Game.Random.Float( -6f, 6f ),
				Game.Random.Float( -6f, 6f ),
				0f
			);

			go.Transform.World = new Transform( basePos + jitter, Rotation.Identity );

			var pickup = go.Components.Create<GroundItemPickup>();
			pickup.ItemId = itemId;
			pickup.Amount = dropNow;

			go.NetworkSpawn();

			remaining -= dropNow;
		}
	}
	
	
	
	
}
