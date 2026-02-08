using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace Astrofront;

/// <summary>
/// Component à mettre DANS LA SCÈNE (Lobby, Astrofront, etc.).
/// Il applique automatiquement les rules :
/// - Côté HOST : ApplyHost(player) une seule fois par player spawné
/// - Côté CLIENT local (owner) : ApplyLocal(player) une seule fois sur le local player
///
/// ✅ En plus, il centralise l'initialisation des "registries" (motors, etc.)
/// pour éviter une explosion de bootstraps.
/// </summary>
public sealed class ModeRulesBootstrap : Component
{
	public enum ModeId
	{
		Lobby,
		Astrofront
	}

	[Property] public ModeId Mode { get; set; } = ModeId.Lobby;

	// ✅ FIX : Owner.Id est un Guid, pas un int
	private readonly HashSet<System.Guid> _hostApplied = new();
	private bool _localApplied;

	// ✅ Init registries une seule fois par scène
	private bool _registriesInitialized;

	protected override void OnStart()
	{
		EnsureRegistries();
	}

	protected override void OnUpdate()
	{
		// Safe: si ce component est ajouté tard ou si la scène a un timing chelou
		EnsureRegistries();

		ApplyHostRulesIfNeeded();
		ApplyLocalRulesIfNeeded();
	}

	private void EnsureRegistries()
	{
		if ( _registriesInitialized )
			return;

		_registriesInitialized = true;

		// 1) Core (walk/fly, etc.)
		CoreBootstrap.EnsureInitialized();

		// 2) Mode-specific
		switch ( Mode )
		{
			case ModeId.Lobby:
				// Lobby: si tu veux des motors spécifiques lobby plus tard, tu les mets ici.
				Log.Info( "[ModeRulesBootstrap] Mode=LObby registries initialized." );
				break;

			case ModeId.Astrofront:
				// Astrofront: register des motors spécifiques au mode
				MovementMotorRegistry.Register( "astrofront_fly", () => new FlyMotor() );
				Log.Info( "[ModeRulesBootstrap] Mode=Astrofront registries initialized: astrofront_fly" );
				break;
		}
	}

	private void ApplyHostRulesIfNeeded()
	{
		if ( !Networking.IsHost ) return;

		var players = Scene.GetAllComponents<PlayerState>()
			.Select( ps => ps?.GameObject )
			.Where( go => go != null && go.Network?.Owner != null );

		foreach ( var player in players )
		{
			var ownerId = player.Network.Owner.Id; // Guid

			if ( _hostApplied.Contains( ownerId ) )
				continue;

			_hostApplied.Add( ownerId );

			switch ( Mode )
			{
				case ModeId.Lobby:
					Lobby_Rules.ApplyHost( player );
					break;

				case ModeId.Astrofront:
					Astrofront_Rules.ApplyHost( player );
					break;
			}
		}
	}

	private void ApplyLocalRulesIfNeeded()
	{
		if ( _localApplied ) return;

		var localPlayer = Scene.GetAllComponents<PlayerState>()
			.FirstOrDefault( ps => ps != null && !ps.IsProxy && ps.GameObject.Tags.Has( "localplayer" ) )
			?.GameObject;

		if ( localPlayer == null )
			return;

		_localApplied = true;

		switch ( Mode )
		{
			case ModeId.Lobby:
				Lobby_SboxController_Rules.ApplyLocal( localPlayer );
				break;

			case ModeId.Astrofront:
				Astrofront_Controller_Rules.ApplyLocal( localPlayer );
				break;
		}
	}
}
