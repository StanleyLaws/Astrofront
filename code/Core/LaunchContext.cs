namespace Astrofront;

public static class LaunchContext
{
    /// <summary>
    /// Mis à true juste avant de charger game.scene depuis le menu.
    /// Réinitialisé à false dès l’entrée dans game.scene.
    /// </summary>
    public static bool FromMenu { get; set; } = false;
}
