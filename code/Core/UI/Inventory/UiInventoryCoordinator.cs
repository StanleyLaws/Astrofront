namespace Astrofront;

public static class UiInventoryCoordinator
{
    /// <summary>
    /// Appelé depuis UiInteractionController lorsque le joueur presse "inv".
    /// Ouvre toujours le panel "ground items" + l'inventaire interactif.
    /// </summary>
    public static void HandleInvPressed()
    {
        bool anyOpen =
            (InventoryManagePanel.Instance?.IsOpen == true) ||
            (GroundItemsPanel.Instance?.IsOpen == true);

        if ( anyOpen )
        {
            UiModalController.CloseAllUi(); // ferme panels + clear drag
            return;
        }

        // Ouvre toujours le loot panel (même vide) + l'inventaire
        GroundItemsService.OpenLootForLocal();
        InventoryManagePanel.Show();
    }
}
