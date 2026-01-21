namespace Astrofront;

public static class UiInventoryCoordinator
{
    /// <summary>
    /// Appelé depuis UiInteractionController lorsque le joueur presse "inv".
    /// </summary>
    public static void HandleInvPressed()
    {
        // Vérifie si des ressources sont lootables autour du joueur
        bool hasLootNearby = PanelTestService.HasLootNearbyForLocal();

        if ( hasLootNearby )
        {
            // On ouvre le panel de loot + le panel d'inventaire interactif
            PanelTestService.OpenLootForLocal();

            InventoryManagePanel.Show();
        }
        else
        {
            // On toggle uniquement l'inventaire interactif
            if ( InventoryManagePanel.Instance?.IsOpen == true )
                InventoryManagePanel.Hide();
            else
                InventoryManagePanel.Show();
        }
    }
}
