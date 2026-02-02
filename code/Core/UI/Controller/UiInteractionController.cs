using Sandbox;

namespace Astrofront;

public sealed class UiInteractionController : Component
{
	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		// Si une UI modale est ouverte, on laisse ESC gérer la fermeture
		// (et on évite de traiter d'autres inputs UI qui pourraient ré-ouvrir quelque chose).
		if ( Input.EscapePressed )
		{
			if ( UiModalController.IsUiLockedLocal )
			{
				UiModalController.CloseAllUi();
				Input.EscapePressed = false;
			} 
			return;
		}

		// INV toggle (inventaire interactif)
		if ( Input.Pressed( InputActions.InventoryToggle ) )
		{
			// Vérifie la permission de mode via PlayerUiContext
			var ctx = GameObject.Components.Get<PlayerUiContext>( FindMode.EverythingInSelfAndDescendants );

			bool allowed = ctx?.inventorymanagepanel ?? true; // défaut permissif si ctx absent
			if ( allowed )
			{
				UiInventoryCoordinator.HandleInvPressed();

				// évite que d'autres systèmes réagissent dans la même frame
				Input.EscapePressed = false;
			}
			else
			{
				Log.Info( "[UiInteractionController] InventoryManagePanel disabled by rules (PlayerUiContext)." );
			}

			return;
		}
	}
}
