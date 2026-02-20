using Sandbox;

namespace Astrofront;

public static class Lobby_Rules
{
	public static void ApplyHost( GameObject player )
	{
		if ( player == null ) return;
		if ( !Networking.IsHost ) return;

		var ctx = player.Components.Get<PlayerUiContext>( FindMode.EverythingInSelfAndDescendants );
		if ( ctx == null )
		{
			Log.Warning( "[Lobby_Rules] PlayerUiContext introuvable sur le player." );
			return;
		}

		// UI
		ctx.SetUiHost(
			vitalbarEnabled: true,
			invhudEnabled: true,
			inventoryManagePanelEnabled: false
		);

		// Gameplay permissions
		ctx.SetGameplayHost(
			pvpEnabled: false,
			useEnabled: true
		);

		// View permissions
		ctx.SetViewHost(
			firstPerson: true,
			thirdPerson: true,
			viewModel: true,
			legsInFp: false
		);

		Log.Info( "[Lobby_Rules] Applied host rules." );
	}
}
