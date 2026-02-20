using Sandbox;

namespace Astrofront;

public static class Astrofront_Rules
{
	public static void ApplyHost( GameObject player )
	{
		if ( player == null ) return;
		if ( !Networking.IsHost ) return;

		var ctx = player.Components.Get<PlayerUiContext>( FindMode.EverythingInSelfAndDescendants );
		if ( ctx == null )
		{
			Log.Warning( "[Astrofront_Rules] PlayerUiContext introuvable sur le player." );
			return;
		}

		// UI
		ctx.SetUiHost(
			vitalbarEnabled: true,
			invhudEnabled: true,
			inventoryManagePanelEnabled: true
		);

		// Gameplay permissions
		ctx.SetGameplayHost(
			pvpEnabled: true,
			useEnabled: true
		);

		// View permissions
		ctx.SetViewHost(
			firstPerson: true,
			thirdPerson: true,
			viewModel: true,
			legsInFp: false
		);

		Log.Info( "[Astrofront_Rules] Applied host rules." );
	}
}
