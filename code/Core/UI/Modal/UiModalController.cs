using Sandbox;

namespace Astrofront;

/// <summary>
/// Contrôleur global des UIs "modales" (inventaire, loot/ground items, etc.).
/// - Expose un flag global IsUiLockedLocal (pour InputGate / movement / caméra).
/// - Fournit un point central pour fermer toutes les UIs modales.
/// </summary>
public sealed class UiModalController : Component
{
    public static UiModalController Instance { get; private set; }

    /// <summary>
    /// Est-ce que ce composant doit écouter certains inputs ? (réservé / futur)
    /// </summary>
    [Property] public bool ListenInput { get; set; } = true;

    /// <summary>
    /// Flag global : au moins une UI modale (loot / inventaire) est ouverte.
    /// Utilisé pour bloquer le gameplay (movement/cam) via InputGate.
    /// </summary>
    public static bool IsUiLockedLocal =>
        (GroundItemsPanel.Instance?.IsOpen ?? false) ||
        (InventoryManagePanel.Instance?.IsOpen ?? false);

    protected override void OnStart()
    {
        if ( Instance == null )
        {
            Instance = this;
        }
        else if ( Instance != this )
        {
            Log.Warning( "[UiModalController] Une autre instance existe déjà, celle-ci sera ignorée." );
        }
    }

    protected override void OnDestroy()
    {
        if ( Instance == this )
            Instance = null;
    }

    protected override void OnUpdate()
    {
        if ( IsProxy ) return;

        // Pour l'instant: ce controller ne lit pas d'input.
        // L'input est géré par UiInteractionController / UiInventoryCoordinator.
    }

    /// <summary>Ferme toutes les UIs modales concernées.</summary>
    public static void CloseAllUi()
    {
        if ( GroundItemsPanel.Instance?.IsOpen == true )
        {
            GroundItemsPanel.Hide();
        }

        if ( InventoryManagePanel.Instance?.IsOpen == true )
        {
            InventoryManagePanel.Hide();
        }

        UiDragContext.Clear();

        Log.Info( "[UiModalController] CloseAllUi()" );
    }
}
