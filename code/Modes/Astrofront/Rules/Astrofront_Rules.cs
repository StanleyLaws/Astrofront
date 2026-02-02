using Sandbox;

namespace Astrofront;

/// <summary>
/// Règles agnostiques controller pour Astrofront (UI + permissions gameplay).
/// À appeler côté HOST après spawn.
/// </summary>
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

		// UI (Astrofront)
		ctx.SetUiHost(
			vitalbarEnabled: true,
			invhudEnabled: true,
			inventoryManagePanelEnabled: true
		);

		// Gameplay permissions (Astrofront)
		ctx.SetGameplayHost(
			pvpEnabled: true,
			useEnabled: true
		);

		// =========================
		// ENERGY (Astrofront utilise l'énergie)
		// =========================
		var ps = player.Components.Get<PlayerState>( FindMode.EverythingInSelfAndDescendants );
		if ( ps != null )
		{
			ps.SetMaxEnergyHost( 100 );
			ps.SetEnergyHost( ps.MaxEnergy ); // spawn full
		}
		else
		{
			Log.Warning( "[Astrofront_Rules] PlayerState introuvable (Energy non configurable)." );
		}

		var energy = player.Components.Get<PlayerEnergySystem>( FindMode.EverythingInSelfAndDescendants );
		if ( energy != null )
		{
			// Active la simulation énergie (drains/regen)
			energy.SetEnergyEnabledHost( true );

			// Regen passive (énergie/sec) - seulement hors drain
			energy.SetPassiveRegenHost( perSecond: 6f, regenWhileDraining: false );

			// Plancher
			energy.SetMinEnergyHost( 0 );
		}
		else
		{
			Log.Warning( "[Astrofront_Rules] PlayerEnergySystem introuvable." );
		}

		// =========================
		// SPRINT -> ENERGY (driver s&box)
		// =========================
		var sprint = player.Components.Get<SboxSprintEnergyDriver>( FindMode.EverythingInSelfAndDescendants );
		if ( sprint != null )
		{
			// enabled: true
			// drainPerSecond: 15 énergie/sec pendant sprint
			// minEnergyToSprint: 1 (ou mets 5 si tu veux éviter le "micro sprint")
			// useMinThreshold: true => sprint coupé dès qu'on passe sous MinEnergyToSprint
			sprint.ConfigureHost(
				enabled: true,
				drainPerSecond: 15f,
				minEnergyToSprint: 1,
				useMinThreshold: true
			);
		}
		else
		{
			Log.Warning( "[Astrofront_Rules] SboxSprintEnergyDriver introuvable (sprint drain off)." );
		}

		Log.Info( "[Astrofront_Rules] Applied host rules." );
	}
}
