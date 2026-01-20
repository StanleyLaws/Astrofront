using Sandbox;

namespace Astrofront;

/// Charge une scène au démarrage si on passe un argument de ligne de commande.
/// Exemple d'usage: lancer le serveur avec +astro_start game
public sealed class StartupRouter : Component 
{
    [Property] public string MenuScenePath { get; set; } = "scenes/menu.scene";
    [Property] public string GameScenePath { get; set; } = "scenes/AF_1x1.scene";

    // ConVar lisible depuis la ligne de commande: +astro_start menu  (ou game)
    [ConVar("astro_start")]
    public static string StartTarget { get; set; } = ""; // "", "menu", "game"
	[ConVar( "astro_scene" )]
	public static string StartScenePath { get; set; } = ""; // ex: "scenes/lobby.scene"

    protected override void OnStart()
{
    // 1) priorité : +astro_scene "scenes/lobby.scene" ou "scenes/game.scene"
    var scenePath = (StartScenePath ?? "").Trim();
    if ( !string.IsNullOrEmpty( scenePath ) )
    {
        var res = ResourceLibrary.Get<SceneFile>( scenePath );
        Log.Info($"[StartupRouter] astro_scene='{scenePath}' -> {(res != null ? "OK" : "NOT FOUND")}");
        if ( res != null ) { Scene.Load( res ); return; }
    }

    // 2) fallback : +astro_start menu|game (optionnel)
    var target = (StartTarget ?? "").Trim().ToLowerInvariant();
    if ( string.IsNullOrEmpty( target ) )
    {
        Log.Info("[StartupRouter] no astro_scene, no astro_start -> stay on current scene.");
        return;
    }

    if ( target == "menu" )
    {
        var res = ResourceLibrary.Get<SceneFile>( MenuScenePath );
        Log.Info($"[StartupRouter] astro_start='menu' -> {(res != null ? "OK" : "NOT FOUND")}");
        if ( res != null ) { Scene.Load( res ); return; }
    }
    else if ( target == "game" )
    {
        var res = ResourceLibrary.Get<SceneFile>( GameScenePath );
        Log.Info($"[StartupRouter] astro_start='game' -> {(res != null ? "OK" : "NOT FOUND")}");
        if ( res != null ) { Scene.Load( res ); return; }
    }
    else
    {
        Log.Warning($"[StartupRouter] astro_start inconnu: '{target}' (attendu: menu|game)");
    }
}


}
