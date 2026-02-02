using Sandbox;

namespace Astrofront;

/// <summary>
/// Règles agnostiques (pas liées au controller) pour le Lobby.
/// Configure uniquement UI + permissions gameplay + systèmes globaux.
/// </summary>
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

		// =========================
		// UI
		// =========================
		ctx.SetUiHost(
			vitalbarEnabled: true,
			invhudEnabled: true,
			inventoryManagePanelEnabled: false
		);

		// =========================
		// GAMEPLAY PERMISSIONS
		// =========================
		ctx.SetGameplayHost(
			pvpEnabled: false,
			useEnabled: true
		);

		// =========================
		// ENERGY SYSTEM (désactivé en Lobby)
		// =========================

		var energy = player.Components.Get<PlayerEnergySystem>( FindMode.EverythingInSelfAndDescendants );
		if ( energy != null )
		{
			// Coupe complètement la simulation énergie
			energy.SetEnergyEnabledHost( false );
		}

		var sprint = player.Components.Get<SboxSprintEnergyDriver>( FindMode.EverythingInSelfAndDescendants );
		if ( sprint != null )
		{
			// Sprint autorisé MAIS ne consomme rien
			sprint.ConfigureHost(
				enabled: false,
				drainPerSecond: 0f,
				minEnergyToSprint: 0,
				useMinThreshold: false
			);
		}

		Log.Info( "[Lobby_Rules] Applied host rules." );
	}
}
