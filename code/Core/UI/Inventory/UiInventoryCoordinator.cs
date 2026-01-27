namespace Astrofront;

public static class UiInventoryCoordinator
{
    public static void HandleInvPressed()
    {
        // toggle
        if ( InventoryManagePanel.Instance?.IsOpen == true || GroundItemsPanel.Instance?.IsOpen == true )
        {
            UiModalController.CloseAllUi();
            return;
        }

        InventoryManagePanel.Show();

        // Ouvre loot + premier scan
        GroundItemsService.OpenLootForLocal();
    }
}
 