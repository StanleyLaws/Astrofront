using Sandbox;

namespace Astrofront;

public static class Astrofront_Controller_Rules
{
	public static void ApplyHost( GameObject player )
	{
		if ( player == null ) return;
		if ( !Networking.IsHost ) return;

		// PlayerState (énergie autoritaire)
		var ps = player.Components.Get<PlayerState>( FindMode.EverythingInSelfAndDescendants );
		if ( ps != null )
		{
			ps.SetMaxEnergyHost( 100 );
			ps.SetEnergyHost( ps.MaxEnergy );
		}
		else
		{
			Log.Warning( "[Astrofront_Controller_Rules] PlayerState introuvable." );
		}

		// EnergySystem (simulation autoritaire)
		var energy = player.Components.Get<PlayerEnergySystem>( FindMode.EverythingInSelfAndDescendants );
		if ( energy != null )
		{
			energy.SetEnergyEnabledHost( true );
			energy.SetPassiveRegenHost( perSecond: 6f, regenWhileDraining: false );
			energy.SetMinEnergyHost( 0 );
			energy.ClearDrainHost( "sprint" );
		}
		else
		{
			Log.Warning( "[Astrofront_Controller_Rules] PlayerEnergySystem introuvable." );
		}

		// Controller (tuning gameplay autoritaire)
		var ctrl = player.Components.Get<MyCustomController>( FindMode.EverythingInSelfAndDescendants );
		if ( ctrl == null )
		{
			Log.Warning( "[Astrofront_Controller_Rules] MyCustomController introuvable (HOST)." );
			return;
		}

		ctrl.WalkSpeed = 220f;
		ctrl.SprintSpeed = 320f;
		ctrl.SlowWalkSpeed = 120f;

		ctrl.Gravity = 900f;
		ctrl.Acceleration = 10f;
		ctrl.GroundFriction = 6f;
		ctrl.StopSpeed = 120f;

		ctrl.JumpSpeed = 360f;
		ctrl.CoyoteTime = 0.12f;
		ctrl.JumpBuffer = 0.12f;

		ctrl.StandRadius = 16f;
		ctrl.StandHeight = 72f;
		ctrl.StepHeight = 18f;

		ctrl.DuckInSpeed = 12f;
		ctrl.DuckOutSpeed = 40f;
		ctrl.DuckRadiusScale = 0.80f;
		ctrl.DuckHeightScale = 0.60f;
		ctrl.DuckSpeedMultiplier = 0.55f;

		ctrl.AlignToCameraYaw = true;
		ctrl.FirstPersonAlwaysAlignYaw = true;
		ctrl.ThirdPersonFacing = MyCustomController.ThirdPersonFacingMode.CameraYaw;
		ctrl.AlignRotateSpeed = 12f;

		// Sprint energy (⚠ int)
		ctrl.SprintUsesEnergy = true;
		ctrl.SprintEnergyDrainPerSecond = 15;
		ctrl.MinEnergyToStartSprint = 1;
		ctrl.SprintDrainMinSpeed = 20f;

		// Jump energy (⚠ int)
		ctrl.JumpUsesEnergy = true;
		ctrl.JumpEnergyCost = 8;
		ctrl.MinEnergyToJump = 8;
		ctrl.JumpDetectedMinDeltaVelZ = 80f;

		ctrl.UseWalkMotor();

		Log.Info( "[Astrofront_Controller_Rules] Applied HOST controller rules." );
	}

	public static void ApplyLocal( GameObject player )
	{
		if ( player == null ) return;

		// INPUT bindings (local)
		var input = player.Components.Get<PlayerMovementInput>( FindMode.EverythingInSelfAndDescendants );
		if ( input != null )
		{
			input.SprintAction = "Run";
			input.SlowWalkAction = "SlowWalk";
		}

		// Cam tuning local éventuel
		var camBrain = player.Components.Get<MyCustomControllerCamera>( FindMode.EverythingInSelfAndDescendants );
		if ( camBrain != null )
		{
			// rien par défaut
		}

		Log.Info( "[Astrofront_Controller_Rules] Applied LOCAL controller rules." );
	}
}
