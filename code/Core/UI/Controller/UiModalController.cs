using Sandbox;

namespace Astrofront;

/// <summary>
/// Contrôleur global des UIs modales (inventaire, loot, etc.)
/// - IsUiLockedLocal : sert à bloquer movement/cam (dans ton gameplay code)
/// - Applique l'état du curseur via Mouse.Visibility (API récente)
/// - CloseAllUi : point central pour fermer
/// </summary>
public sealed class UiModalController : Component
{
	public static UiModalController Instance { get; private set; }

	/// <summary>
	/// Vrai si une UI modale est ouverte (local).
	/// </summary>
	public static bool IsUiLockedLocal =>
		(InventoryManagePanel.Instance?.IsOpen ?? false) ||
		(GroundItemsPanel.Instance?.IsOpen ?? false);

	protected override void OnStart()
	{
		if ( Instance == null ) Instance = this;
	}

	protected override void OnDestroy()
	{
		if ( Instance == this ) Instance = null;

		// On laisse un état propre si l'objet est détruit
		ApplyCursorState( false );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		// Option 1 (safe) : on applique chaque frame.
		// Comme ça même si un panel s'ouvre/ferme autrement, ça reste cohérent.
		ApplyCursorState( IsUiLockedLocal );
	}

	/// <summary>
	/// Ferme toutes les UIs modales concernées.
	/// </summary>
	public static void CloseAllUi()
	{
		InventoryManagePanel.Hide();
		GroundItemsPanel.Hide();

		UiDragContext.Clear();

		ApplyCursorState( false );
		Log.Info( "[UiModalController] CloseAllUi()" );
	}

	/// <summary>
	/// Appelé quand tu ouvres/fermes une UI modale.
	/// Visible = UI ouverte, Hidden = gameplay.
	/// </summary>
	public static void ApplyCursorState( bool uiOpen )
	{
		// API récente: Mouse.Visibility remplace Mouse.Visible :contentReference[oaicite:2]{index=2}
		Mouse.Visibility = uiOpen ? MouseVisibility.Visible : MouseVisibility.Hidden; // :contentReference[oaicite:3]{index=3}
	}
}
