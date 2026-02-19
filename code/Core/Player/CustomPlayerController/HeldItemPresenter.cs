using Sandbox;

namespace Astrofront;

/// <summary>
/// Pont runtime: Inventory -> Prefab instancié (held item).
/// - Clone le prefab (ItemDefinition.ItemPrefab) quand un item est sélectionné
/// - Détruit l'ancien held item quand on change de slot
/// - L'instance held est local-only (NetworkMode.Never)
/// - Pour l'instant: le "held world model" n'est jamais visible (TP ni FP)
/// 
/// NOTE: La visibilité du corps joueur en FP est gérée par LocalFirstPersonBodyVisibility.
/// </summary>
[Group( "Astrofront" )]
[Title( "Held Item Presenter" )]
public sealed class HeldItemPresenter : Component
{
	[Property, Group( "Refs" )]
	public GameObject AttachPoint { get; set; }

	[Property, Group( "Tuning" )]
	public Vector3 LocalOffset { get; set; } = Vector3.Zero;

	[Property, Group( "Tuning" )]
	public Angles LocalAngles { get; set; } = Angles.Zero;

	[Property, Group( "Tags" )]
	public string ViewModelTag { get; set; } = "viewmodel";

	[Property, Group( "Debug" )]
	public bool DebugLogs { get; set; } = true;

	private InventoryComponent _inv;
	private GameObject _heldInstance;
	private int _lastSelected = -999;

	protected override void OnStart()
	{
		_inv = Components.Get<InventoryComponent>( FindMode.EverythingInSelfAndDescendants );
		if ( _inv == null )
		{
			if ( DebugLogs ) Log.Warning( "[HeldItem] No InventoryComponent found on player." );
			return;
		}

		_inv.SelectionChanged += OnSelectionChanged;

		// Équipe l'item courant au start
		OnSelectionChanged( _inv.SelectedIndex );
	}

	protected override void OnDestroy()
	{
		if ( _inv != null )
			_inv.SelectionChanged -= OnSelectionChanged;

		// Safe destroy
		ClearHeldSafe();
	}

	protected override void OnUpdate()
	{
		// Robustesse: s'assure que le held reste invisible même si un autre script réactive un renderer.
		ApplyHeldVisibilityAlways();
	}

	protected override void OnPreRender()
	{
		// Dernier writer avant rendu
		ApplyHeldVisibilityAlways();
	}

	private void OnSelectionChanged( int idx )
	{
		if ( _inv == null ) return;

		if ( idx == _lastSelected && _heldInstance.IsValid() )
			return;

		_lastSelected = idx;

		if ( idx == 0 )
		{
			if ( DebugLogs ) Log.Info( "[HeldItem] Selected HANDS (slot 0) -> clear held." );
			ClearHeldSafe();
			return;
		}

		var slots = _inv.GetSlotsSnapshot();
		if ( slots == null || idx < 0 || idx >= slots.Count )
		{
			if ( DebugLogs ) Log.Warning( $"[HeldItem] Invalid slot index {idx}." );
			ClearHeldSafe();
			return;
		}

		var (itemId, amount) = slots[idx];

		if ( string.IsNullOrEmpty( itemId ) || amount <= 0 )
		{
			if ( DebugLogs ) Log.Info( $"[HeldItem] Slot {idx} empty -> clear held." );
			ClearHeldSafe();
			return;
		}

		SpawnHeldFromRegistry( itemId );
	}

	private void SpawnHeldFromRegistry( string itemId )
	{
		ClearHeldSafe();

		var prefab = ItemRegistry.GetItemPrefab( itemId );
		if ( prefab == null )
		{
			if ( DebugLogs ) Log.Warning( $"[HeldItem] No prefab in registry for item '{itemId}'." );
			return;
		}

		var parent = AttachPoint ?? GameObject;

		_heldInstance = prefab.Clone();
		_heldInstance.Name = $"held_{itemId}";
		_heldInstance.SetParent( parent, keepWorldPosition: false );

		// Local-only
		_heldInstance.NetworkMode = NetworkMode.Never;

		_heldInstance.LocalPosition = LocalOffset;
		_heldInstance.LocalRotation = Rotation.From( LocalAngles );
		_heldInstance.Enabled = true;

		// IMPORTANT: held world model jamais visible
		ApplyHeldVisibilityAlways();

		if ( DebugLogs )
			Log.Info( $"[HeldItem] Spawned '{_heldInstance.Name}' from prefab for '{itemId}'." );
	}

	/// <summary>
	/// Held item : désactive tous les ModelRenderer non taggés viewmodel.
	/// Donc: pas de worldmodel en main (TP/FP).
	/// </summary>
	private void ApplyHeldVisibilityAlways()
	{
		if ( !_heldInstance.IsValid() ) return;

		foreach ( var r in _heldInstance.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( r == null || r.GameObject == null ) continue;

			bool isViewModel = r.GameObject.Tags.Has( ViewModelTag );
			if ( !isViewModel )
				r.Enabled = false;
		}
	}

	private void ClearHeldSafe()
	{
		if ( !_heldInstance.IsValid() )
		{
			_heldInstance = null;
			return;
		}

		var toDestroy = _heldInstance;
		_heldInstance = null;

		// 1) Désactive immédiatement pour stopper les updates/dirty
		toDestroy.Enabled = false;

		// 2) Détruit fin de frame (évite OnDirty/attachments sur objet déjà "cassé")
		DestroyEndOfFrame( toDestroy );

		async void DestroyEndOfFrame( GameObject go )
		{
			await GameTask.Delay( 1 );
			if ( go.IsValid() )
				go.Destroy();
		}
	}
}
