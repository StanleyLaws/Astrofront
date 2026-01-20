using Sandbox;
using System.Linq;

namespace Astrofront;

public sealed class FallbackCamera : Component
{
    [Property] public CameraComponent Camera { get; set; }

    protected override void OnStart()
    {
        Camera ??= Components.Get<CameraComponent>( FindMode.EverythingInSelf );
        if ( Camera == null )
            Log.Error("[FallbackCamera] Pas de CameraComponent.");
    }

    protected override void OnUpdate()
    {
        if ( Camera == null ) return;

        bool hasLocalPlayer =
            Scene.GetAllComponents<PlayerState>()
                 .Any( ps => ps is { } && !ps.IsProxy );

        Camera.Enabled = !hasLocalPlayer;
    }
}
