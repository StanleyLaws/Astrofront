using Sandbox;

namespace Astrofront;

public sealed class OwnerOnlyCamera : Component
{
    [Property] public CameraComponent Camera { get; set; }

    protected override void OnStart()
    {
        if ( Camera == null )
            Camera = Components.Get<CameraComponent>( FindMode.InSelf | FindMode.InChildren );

        // La caméra du player n’est active que pour le client OWNER
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
		if ( IsProxy ) return;

		// NEW: UI lock -> ne pas lire la souris / ne pas bouger la caméra
		if ( LootController.IsUiLockedLocal )
			return;

		// ... reste inchangé ...
	}

	
	
}
