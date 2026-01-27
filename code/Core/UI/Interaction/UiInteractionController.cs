using Sandbox;

namespace Astrofront;

public sealed class UiInteractionController : Component
{
	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		// INV toggle
		if ( Input.Pressed( InputActions.InventoryToggle ) )
		{
			UiInventoryCoordinator.HandleInvPressed();

			// évite que d'autres systèmes réagissent dans la même frame
			Input.EscapePressed = false;
			return;
		}

		// ESC close
		if ( Input.EscapePressed )
		{
			if ( UiModalController.IsUiLockedLocal )
			{
				UiModalController.CloseAllUi();
				Input.EscapePressed = false;
			}
		}
	}
}
