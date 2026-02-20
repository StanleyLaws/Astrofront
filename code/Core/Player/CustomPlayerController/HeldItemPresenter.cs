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

	private bool _warnedMissingInv;

	protected override void OnStart()
	{
		AttachPoint ??= GameObject;

		if ( !TryBindInventory() )
		{
			// Certains setups ajoutent/configurent des comps après spawn => retry quelques frames.
			RetryBindInventory();
		}
	}

	protected override void OnDestroy()
	{
		UnbindInventory();
		ClearHeldSafe();
	}

	protected override void OnUpdate()
	{
		// Si jamais l'inventory apparaît plus tard (ou a été recréé), on rebind.
		if ( _inv == null )
		{
			TryBindInventory();
		}

		ApplyHeldVisibilityAlways();
	}

	protected override void OnPreRender()
	{
		ApplyHeldVisibilityAlways();
	}

	// =========================================================
	// Inventory binding
	// =========================================================

	private void UnbindInventory()
	{
		if ( _inv != null )
			_inv.SelectionChanged -= OnSelectionChanged;

		_inv = null;
	}

	private bool TryBindInventory()
	{
		if ( _inv != null )
			return true;

		// 1) Essai direct (au cas où c'est sur le même GO / parent / enfant)
		_inv = Components.Get<InventoryComponent>( FindMode.EverythingInSelfAndAncestors )
			?? Components.Get<InventoryComponent>( FindMode.EverythingInSelfAndDescendants );

		// 2) Cas courant chez toi: Inventory est sur un GO sibling (Core) => chercher depuis le ROOT du player.
		_inv ??= FindOnPrefabRoot<InventoryComponent>();

		if ( _inv == null )
		{
			if ( DebugLogs && !_warnedMissingInv )
			{
				_warnedMissingInv = true;
				Log.Warning( "[HeldItem] No InventoryComponent found on player (searched self/ancestors/descendants + prefab root descendants)." );
			}

			return false;
		}

		_warnedMissingInv = false;

		_inv.SelectionChanged += OnSelectionChanged;

		// Équipe l'item courant au bind
		OnSelectionChanged( _inv.SelectedIndex );

		if ( DebugLogs )
			Log.Info( "[HeldItem] Bound to InventoryComponent." );

		return true;
	}

	private async void RetryBindInventory()
	{
		// Try for a short time without spamming.
		for ( int i = 0; i < 30; i++ ) // ~30 frames
		{
			if ( !IsValid ) return;

			if ( TryBindInventory() )
				return;

			await Task.Yield();
		}

		// Toujours pas trouvé: on laisse le warning déjà log.
	}

	private T FindOnPrefabRoot<T>() where T : Component
	{
		var root = GetPrefabRoot();
		if ( root == null ) return null;

		return root.Components.Get<T>( FindMode.EverythingInSelfAndDescendants );
	}

	private GameObject GetPrefabRoot()
	{
		var go = GameObject;
		if ( go == null ) return null;

		// Remonte tout en haut de la hiérarchie (root du prefab en runtime)
		while ( go.Parent.IsValid() )
			go = go.Parent;

		return go;
	}

	// =========================================================
	// Inventory events
	// =========================================================

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

	// =========================================================
	// Held spawning
	// =========================================================

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

		toDestroy.Enabled = false;

		DestroyEndOfFrame( toDestroy );

		async void DestroyEndOfFrame( GameObject go )
		{
			await GameTask.Delay( 1 );
			if ( go.IsValid() )
				go.Destroy();
		}
	}
}
