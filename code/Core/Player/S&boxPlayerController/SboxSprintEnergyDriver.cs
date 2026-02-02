using Sandbox;
using System;

namespace Astrofront;

/// <summary>
/// Pont entre le PlayerController S&box et le PlayerEnergySystem.
/// - Draine l'énergie pendant le sprint (drain continu)
/// - Empêche le sprint si l'énergie est trop basse
/// - Configurable par les Rules (pas via des valeurs "hard" dans le prefab)
///
/// À mettre sur player_core.prefab (même GO que PlayerController).
/// </summary>
public sealed class SboxSprintEnergyDriver : Component
{
	[Property] public PlayerController Controller { get; set; }
	[Property] public PlayerEnergySystem EnergySystem { get; set; }
	[Property] public PlayerState PlayerState { get; set; }

	// =========================
	// CONFIG (pilotée par Rules)
	// =========================

	[Sync( SyncFlags.FromHost )]
	public bool SprintEnergyEnabled { get; private set; } = true;

	[Sync( SyncFlags.FromHost )]
	public float SprintDrainPerSecond { get; private set; } = 15f;

	[Sync( SyncFlags.FromHost )]
	public int MinEnergyToSprint { get; private set; } = 1;

	[Sync( SyncFlags.FromHost )]
	public bool UseMinEnergyThreshold { get; private set; } = true;

	// =========================
	// Interne
	// =========================

	private float _baseRunSpeed;
	private bool _initialized;

	protected override void OnStart()
	{
		Controller ??= Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants );
		EnergySystem ??= Components.Get<PlayerEnergySystem>( FindMode.EverythingInSelfAndDescendants );
		PlayerState ??= Components.Get<PlayerState>( FindMode.EverythingInSelfAndDescendants );

		if ( Controller == null )
		{
			Log.Warning( "[SboxSprintEnergyDriver] PlayerController introuvable." );
			return;
		}

		_baseRunSpeed = Controller.RunSpeed;
		_initialized = true;
	}

	protected override void OnUpdate()
	{
		if ( !_initialized ) return;
		if ( IsProxy ) return;

		if ( !SprintEnergyEnabled || EnergySystem == null || PlayerState == null || PlayerState.MaxEnergy <= 0 )
		{
			EnergySystem?.SetDrainLocal( "sprint", 0f );
			RestoreRunSpeed();
			return;
		}

		bool sprintInput = Input.Down( Controller.AltMoveButton );

		bool hasEnoughEnergy = PlayerState.Energy >= MinEnergyToSprint;
		bool allowSprint = UseMinEnergyThreshold ? hasEnoughEnergy : (PlayerState.Energy > 0);

		if ( sprintInput && allowSprint )
			EnergySystem.SetDrainLocal( "sprint", SprintDrainPerSecond );
		else
			EnergySystem.SetDrainLocal( "sprint", 0f );

		if ( !allowSprint )
			Controller.RunSpeed = Controller.WalkSpeed;
		else
			RestoreRunSpeed();
	}

	private void RestoreRunSpeed()
	{
		if ( _baseRunSpeed <= 0f && Controller != null )
			_baseRunSpeed = Controller.RunSpeed;

		if ( Controller != null )
			Controller.RunSpeed = _baseRunSpeed;
	}

	// =========================
	// HOST API (appelée depuis Rules)
	// =========================

	public void ConfigureHost( bool enabled, float drainPerSecond, int minEnergyToSprint, bool useMinThreshold = true )
	{
		if ( !Networking.IsHost ) return;

		SprintEnergyEnabled = enabled;
		SprintDrainPerSecond = System.MathF.Max( 0f, drainPerSecond );
		MinEnergyToSprint = System.Math.Max( 0, minEnergyToSprint );
		UseMinEnergyThreshold = useMinThreshold;
	}

	public void CaptureBaseRunSpeedLocal()
	{
		if ( Controller == null ) return;
		_baseRunSpeed = Controller.RunSpeed;
	}
}
