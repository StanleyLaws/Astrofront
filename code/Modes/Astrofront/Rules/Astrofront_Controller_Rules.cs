using Sandbox;

namespace Astrofront;

public static class Astrofront_Controller_Rules
{
	/// Appliqué côté local (client) sur l'objet player.
	/// Configure le controller custom + caméra custom + anim driver + dresser.
	public static void ApplyLocal( GameObject player )
	{
		if ( player == null ) return;

		// =========================
		// INPUT (source)
		// =========================
		var input = player.Components.Get<PlayerMovementInput>( FindMode.EverythingInSelfAndDescendants );
		if ( input == null )
		{
			Log.Warning( "[Astrofront_Controller_Rules] PlayerMovementInput introuvable." );
		}
		else
		{
			// Si tu veux des noms d'actions standardisés
			input.SprintAction = "Run";     // assure-toi que l'action existe dans Input Actions
			input.SlowWalkAction = "SlowWalk"; // idem
		}

		// =========================
		// CONTROLLER (motor + tuning)
		// =========================
		var ctrl = player.Components.Get<MyCustomController>( FindMode.EverythingInSelfAndDescendants );
		if ( ctrl == null )
		{
			Log.Warning( "[Astrofront_Controller_Rules] MyCustomController introuvable." );
			return;
		}

		// Speeds Astrofront
		ctrl.WalkSpeed = 220f;
		ctrl.SprintSpeed = 320f;
		ctrl.SlowWalkSpeed = 120f;

		// Physique / feeling
		ctrl.Gravity = 900f;
		ctrl.Acceleration = 10f;
		ctrl.GroundFriction = 6f;
		ctrl.StopSpeed = 120f;

		// Jump
		ctrl.JumpSpeed = 360f;
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

		// Orientation (évite le "courir de côté")
		ctrl.AlignToCameraYaw = true;
		ctrl.FirstPersonAlwaysAlignYaw = true;
		ctrl.ThirdPersonFacing = MyCustomController.ThirdPersonFacingMode.CameraYaw;
		ctrl.AlignRotateSpeed = 12f;

		// Motor par défaut Astrofront : WALK
		ctrl.UseWalkMotor();

		// ✅ TEST FLY (décommenter temporairement pour valider l’archi)
		// ctrl.SetMotor( new FlyMotor() );

		// =========================
		// CAMERA (custom)
		// =========================
		// Ici tu peux appliquer tes réglages caméra si ton MyCustomControllerCamera expose les props.
		// (Je ne modifie pas ton script caméra ici pour ne pas casser.)
		var camBrain = player.Components.Get<MyCustomControllerCamera>( FindMode.EverythingInSelfAndDescendants );
		if ( camBrain == null )
		{
			// Pas bloquant
			// Log.Warning( "[Astrofront_Controller_Rules] MyCustomControllerCamera introuvable." );
		} 
	}
}
