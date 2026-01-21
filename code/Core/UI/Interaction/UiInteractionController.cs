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
        if ( !Input.Pressed( "inv" ) )
            return;

        bool anyOpen =
            (InventoryManagePanel.Instance?.IsOpen == true) ||
            (PanelTestUI.Instance?.IsOpen == true);

        if ( anyOpen )
        {
            // Fermer les deux
            InventoryManagePanel.Hide();
            PanelTestUI.Hide();

            // On vide la main fantôme pour éviter de conserver
            // des “items virtuels” après fermeture
            UiDragContext.Clear();

            // Ici tu peux aussi remettre la capture souris / contrôle joueur
            // Input.MouseCapture = true;
            return;
        }

        // --- Ouverture des panels ---
        InventoryManagePanel.Show();
        PanelTestService.OpenLootForLocal(); // panel de loot (même s'il est vide, c'est ton inventaire "monde")

        // Ici tu peux aussi libérer la souris
        // Input.MouseCapture = false;
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
            (PanelTestUI.Instance?.IsOpen == true);

        if ( !anyOpen )
            return;

        // Fermer
        InventoryManagePanel.Hide();
        PanelTestUI.Hide();

        // Purge la main fantôme
        UiDragContext.Clear();

        // On "consomme" ESC pour éviter que d'autres systèmes
        // ouvrent un menu par-dessus dans la même frame.
        Input.EscapePressed = false;

        // Éventuellement, remettre le contrôle joueur ici
        // Input.MouseCapture = true;
    }
}
