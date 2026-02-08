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
			input.SprintAction = "Sprint";     // assure-toi que l'action existe dans Input Actions
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

		// =========================
		// ANIMS (tuning global)
		// =========================
		var anim = player.Components.Get<CitizenAnimDriver>( FindMode.EverythingInSelfAndDescendants );
		if ( anim != null )
		{
			// Exemple de tuning global multi-modes (walk/fly/zeroG)
			anim.WalkRunBlendSpeed = 220f;

			// Les poids d’aim peuvent être fixés ici aussi
			anim.AimStrengthEyes = 1f;
			anim.AimStrengthHead = 1f;
			anim.AimStrengthBody = 0.2f;
		}

		// =========================
		// APPARENCE (Dresser)
		// =========================
		var body = player.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		if ( body == null )
		{
			Log.Warning( "[Astrofront_Controller_Rules] SkinnedModelRenderer introuvable (apparence non appliquée)." );
		}
		else
		{
			body.Model = Model.Load( "models/citizen/citizen.vmdl" );

			var dresser = body.Components.Get<Dresser>( FindMode.EverythingInSelfAndDescendants );
			if ( dresser == null )
				dresser = body.Components.Create<Dresser>();

			dresser.BodyTarget = body;

			// ✅ Multi correct : chaque client voit l’avatar du owner réseau
			dresser.Source = Dresser.ClothingSource.OwnerConnection;

			dresser.Apply();
		}

		Log.Info( "[Astrofront_Controller_Rules] Applied local astrofront custom controller settings." );
	}
}
