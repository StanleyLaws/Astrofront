using Sandbox;

namespace Astrofront;

public static class Astrofront_Citizen_Rules
{
	/// Visuel uniquement : citizen model + dresser + citizen anim driver.
	public static void ApplyLocal( GameObject player )
	{
		if ( player == null ) return;

		// -------------------------
		// BODY (citizen model)
		// -------------------------
		var body = player.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		if ( body == null )
		{
			Log.Warning( "[Astrofront_Citizen_Rules] SkinnedModelRenderer introuvable." );
			return;
		}

		body.Model = Model.Load( "models/citizen/citizen.vmdl" );

		// -------------------------
		// DRESSER (wardrobe)
		// -------------------------
		var dresser = body.Components.Get<Dresser>( FindMode.InSelf | FindMode.InChildren );
		if ( dresser == null )
			dresser = body.Components.Create<Dresser>();

		dresser.BodyTarget = body;
		dresser.Source = Dresser.ClothingSource.OwnerConnection; // multi correct
		dresser.Apply();

		// -------------------------
		// ORIENTATION ROOT (turn-in-place & yaw visuel)
		// -------------------------
		// On veut tourner le visuel, pas le root physique.
		var controller = player.Components.Get<MyCustomController>( FindMode.EverythingInSelfAndDescendants );
		if ( controller != null )
			controller.OrientationRoot = body.GameObject;

		// La caméra peut aussi avoir besoin de savoir quel GO tourner en TP.
		var camBrain = player.Components.Get<MyCustomControllerCamera>( FindMode.EverythingInSelfAndDescendants );
		if ( camBrain != null )
			camBrain.OrientationRoot = body.GameObject;

		// -------------------------
		// CITIZEN ANIM DRIVER
		// -------------------------
		var anim = body.Components.Get<CitizenAnimDriver>( FindMode.EverythingInSelfAndDescendants );
		if ( anim == null )
			anim = body.Components.Create<CitizenAnimDriver>();

		// Si tu veux forcer le graph ici (c’est citizen-specific)
		anim.GraphPath = "models/citizen/citizen.vanmgrph";

		// Tuning citizen (par mode)
		anim.WalkRunBlendSpeed = 220f;
		anim.AimStrengthEyes = 1f;
		anim.AimStrengthHead = 1f;
		anim.AimStrengthBody = 0.2f;

		Log.Info( "[Astrofront_Citizen_Rules] Applied local citizen appearance + anim." );
	}
}
