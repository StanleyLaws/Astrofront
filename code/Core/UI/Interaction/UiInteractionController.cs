using Sandbox;

namespace Astrofront;

/// <summary>
/// Lit certains inputs du joueur et ouvre/ferme les panels d'UI globaux.
/// - "inv" : toggle Inventaire + PanelTestUI
/// - "esc" : si Inventaire / Loot ouverts → les ferme + clear main fantôme
/// </summary>
public sealed class UiInteractionController : Component
{
    protected override void OnUpdate()
    {
        if ( IsProxy )
            return;

        HandleInventoryToggle();
        HandleInventoryEscapeClose();
    }

    /// <summary>
    /// Touche "inv" → toggle des deux panels (inventaire + loot).
    /// </summary>
    private void HandleInventoryToggle()
{
    if ( !Input.Pressed( InputActions.InventoryToggle ) )
        return;

    UiInventoryCoordinator.HandleInvPressed();
}


    /// <summary>
    /// Touche "ESC" → si inventaire/loot ouverts, on les ferme au lieu
    /// de laisser ESC ouvrir un autre menu.
    /// </summary>
    private void HandleInventoryEscapeClose()
    {
        if ( !Input.EscapePressed )
            return;

        bool anyOpen =
            (InventoryManagePanel.Instance?.IsOpen == true) ||
            (GroundItemsPanel.Instance?.IsOpen == true);

        if ( !anyOpen )
            return;

		//Fermer
        UiModalController.CloseAllUi();

		// On "consomme" ESC pour éviter que d'autres systèmes
		// ouvrent un menu par-dessus dans la même frame.
		Input.EscapePressed = false;

	}
}
