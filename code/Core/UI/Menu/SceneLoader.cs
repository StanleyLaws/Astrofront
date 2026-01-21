using Sandbox;

namespace Astrofront;

public sealed class SceneLoader : Component
{
    [Property, Title("Game Scene Path")]
    public string GameScenePath { get; set; } = "scenes/AF_1x1.scene";

    public static SceneLoader Instance { get; private set; }

    protected override void OnAwake() => Instance = this;

    public void LoadGameScene()
    {
		
		
		// on va au jeu depuis le menu → marquer le contexte
        LaunchContext.FromMenu = true;
		
		
        // <<< IMPORTANT : rendre la souris au jeu avant de switcher de scène
        Mouse.Visibility = MouseVisibility.Auto;

        var sceneRes = ResourceLibrary.Get<SceneFile>( GameScenePath );
        if ( sceneRes == null )
        {
            Log.Error( $"[SceneLoader] SceneFile introuvable: '{GameScenePath}'" );
            return;
        }

        Log.Info( $"[SceneLoader] Loading scene: {sceneRes.ResourcePath}" );
        Scene.Load( sceneRes );
    }
}
