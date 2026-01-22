global using Sandbox;
global using System.Collections.Generic;
global using System.Linq;

namespace Astrofront;

/// Noms d'actions Input (Core).
/// Le Core définit le "langage" (Inventory, Drop, Use...), pas les règles.
/// Les mini-jeux peuvent ignorer/étendre ces actions.
public static class InputActions
{
    // UI / Menus
    public const string InventoryToggle = "inventory";   // ex: touche I
    public const string Chat = "Chat";
    public const string Scoreboard = "Scoreboard";

    // Interaction
    public const string Use = "Use";
    public const string Drop = "Drop";                  // ex: touche G

    // Combat / souris (souvent universels)
    public const string Attack1 = "Attack1";
    public const string Attack2 = "Attack2";

    // Slots rapides inventaire (si tu en as)
    public const string Slot1 = "Slot1";
    public const string Slot2 = "Slot2";
    public const string Slot3 = "Slot3";
    public const string Slot4 = "Slot4";
    public const string Slot5 = "Slot5";
}
