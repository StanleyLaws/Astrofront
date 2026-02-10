using Sandbox;

namespace Astrofront;

public static class Lobby_Controller_Rules
{
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

		// CONTROLLER
		var ctrl = player.Components.Get<MyCustomController>( FindMode.EverythingInSelfAndDescendants );
		if ( ctrl == null )
		{
			Log.Warning( "[Lobby_Controller_Rules] MyCustomController introuvable." );
			return;
		}

		// Speeds lobby (plus calme)
		ctrl.WalkSpeed = 160f;
		ctrl.SprintSpeed = 220f;
		ctrl.SlowWalkSpeed = 80f;

		// Feeling
		ctrl.Gravity = 900f;
		ctrl.Acceleration = 10f;
		ctrl.GroundFriction = 6f;
		ctrl.StopSpeed = 120f;

		// Jump
		ctrl.JumpSpeed = 260f;
		ctrl.CoyoteTime = 0.12f;
		ctrl.JumpBuffer = 0.12f;

		// Capsule
		ctrl.StandRadius = 16f;
		ctrl.StandHeight = 72f;
		ctrl.StepHeight = 18f;

		// Duck
		ctrl.DuckInSpeed = 12f;
		ctrl.DuckOutSpeed = 40f;
		ctrl.DuckRadiusScale = 0.80f; 
		ctrl.DuckHeightScale = 0.60f;
		ctrl.DuckSpeedMultiplier = 0.55f;

		// Orientation
		ctrl.AlignToCameraYaw = true;
		ctrl.FirstPersonAlwaysAlignYaw = true;
		ctrl.ThirdPersonFacing = MyCustomController.ThirdPersonFacingMode.CameraYaw;
		ctrl.AlignRotateSpeed = 12f;

		// Motor
		ctrl.UseWalkMotor();

		// Anim policy (optionnel : lobby plus “lent” visuellement)
		ctrl.UseAnimationPolicy = true;
		ctrl.PolicyAnimSpeedMultiplier = 0.9f;
		ctrl.ClearMoveStyleOverride();
		ctrl.ClearHoldTypeOverride();

		Log.Info( "[Lobby_Controller_Rules] Applied lobby controller rules." );
	}
}
