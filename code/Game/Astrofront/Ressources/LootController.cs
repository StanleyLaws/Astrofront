using Sandbox;

namespace Astrofront;

/// <summary>
/// Contrôleur simple pour ouvrir/fermer l’UI de loot + inventaire interactif
/// sur la touche "use", et exposer un flag global IsUiLockedLocal.
/// 
/// Toute l’ancienne logique de LootPanel (stacks, TakeAll/TakeOne) est remplacée
/// par PanelTestUI + InventoryManagePanel + UiLootInventoryBridge.
/// </summary>
public sealed class LootController : Component
{
    public static LootController Instance { get; private set; }

    /// <summary>Est-ce que ce composant doit écouter l’input "use" ?</summary>
    [Property] public bool ListenInput { get; set; } = true;

    /// <summary>
    /// Flag global : au moins une UI modale de loot / inventaire est ouverte.
    /// Utilisé pour bloquer le mouvement, scroll d’inventaire, etc.
    /// </summary>
    public static bool IsUiLockedLocal =>
        (PanelTestUI.Instance?.IsOpen ?? false) ||
        (InventoryManagePanel.Instance?.IsOpen ?? false);

    protected override void OnStart()
    {
        if ( Instance == null )
        {
            Instance = this;
        }
        else if ( Instance != this )
        {
            Log.Warning( "[LootController] Une autre instance existe déjà, celle-ci sera ignorée." );
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

    // Ancienne gestion de "use" / LootPanel désactivée.
    // Le ramassage est maintenant géré par PanelTestService + UiInteractionController.
}


    /// <summary>Ferme toutes les UIs concernées.</summary>
    private void CloseAllUi() 
    {
        if ( PanelTestUI.Instance?.IsOpen == true )
        {
            PanelTestUI.Hide();
        }

        if ( InventoryManagePanel.Instance?.IsOpen == true )
        {
            InventoryManagePanel.Hide();
        }

        Log.Info( "[LootController] CloseAllUi()" );
    }
}
