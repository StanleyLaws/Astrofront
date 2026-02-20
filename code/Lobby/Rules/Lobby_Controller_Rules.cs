using Sandbox;

namespace Astrofront;

public static class Lobby_Controller_Rules
{
	public static void ApplyHost( GameObject player )
	{
		if ( player == null ) return;
		if ( !Networking.IsHost ) return;

		// EnergySystem off
		var energy = player.Components.Get<PlayerEnergySystem>( FindMode.EverythingInSelfAndDescendants );
		if ( energy != null )
		{
			energy.SetEnergyEnabledHost( false );
			energy.ClearDrainHost( "sprint" );
		}

		var ctrl = player.Components.Get<MyCustomController>( FindMode.EverythingInSelfAndDescendants );
		if ( ctrl == null )
		{
			Log.Warning( "[Lobby_Controller_Rules] MyCustomController introuvable (HOST)." );
			return;
		}

		// Sprint gratuit (⚠ int)
		ctrl.SprintUsesEnergy = false;
		ctrl.SprintEnergyDrainPerSecond = 0;
		ctrl.MinEnergyToStartSprint = 0;

		// Jump gratuit (⚠ int)
		ctrl.JumpUsesEnergy = false;
		ctrl.JumpEnergyCost = 0;
		ctrl.MinEnergyToJump = 0;

		Log.Info( "[Lobby_Controller_Rules] Applied HOST controller rules." );
	}

	public static void ApplyLocal( GameObject player )
	{
		if ( player == null ) return;

		// INPUT
		var input = player.Components.Get<PlayerMovementInput>( FindMode.EverythingInSelfAndDescendants );
		if ( input != null )
		{
			input.SprintAction = "Run";
			input.SlowWalkAction = "SlowWalk";
		}

		// CONTROLLER feel
		var ctrl = player.Components.Get<MyCustomController>( FindMode.EverythingInSelfAndDescendants );
		if ( ctrl == null )
		{
			Log.Warning( "[Lobby_Controller_Rules] MyCustomController introuvable." );
			return;
		}

		ctrl.WalkSpeed = 160f;
		ctrl.SprintSpeed = 220f;
		ctrl.SlowWalkSpeed = 80f;

		ctrl.Gravity = 900f;
		ctrl.Acceleration = 10f;
		ctrl.GroundFriction = 6f;
		ctrl.StopSpeed = 120f;

		ctrl.JumpSpeed = 260f;
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

		ctrl.UseWalkMotor();

		ctrl.UseAnimationPolicy = true;
		ctrl.PolicyAnimSpeedMultiplier = 0.9f;
		ctrl.ClearMoveStyleOverride();
		ctrl.ClearHoldTypeOverride();

		Log.Info( "[Lobby_Controller_Rules] Applied LOCAL controller rules." );
	}
}
