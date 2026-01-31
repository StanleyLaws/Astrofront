using Sandbox;

namespace Astrofront;

public sealed class OwnerOnlyCamera : Component
{
    [Property] public CameraComponent Camera { get; set; }

    protected override void OnStart()
{
    if ( Camera == null )
        Camera = Components.Get<CameraComponent>( FindMode.InSelf | FindMode.InChildren );

    // Assure l’état correct au démarrage
    if ( Camera != null )
        Camera.Enabled = Network.IsOwner;
}


    protected override void OnEnabled()
    {
        if ( Camera != null )
            Camera.Enabled = Network.IsOwner;
    }

    protected override void OnDisabled()
    {
        if ( Camera != null )
            Camera.Enabled = false;
    }
	
	protected override void OnUpdate()
{
    if ( Camera is null )
        return;

    // Une seule source de vérité : owner local
    bool shouldEnable = Network.IsOwner;

    if ( Camera.Enabled != shouldEnable )
        Camera.Enabled = shouldEnable;
}


	
	
}
