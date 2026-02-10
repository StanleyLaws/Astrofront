using Sandbox;

namespace Astrofront;

public static class Lobby_Citizen_Rules
{
	public static void ApplyLocal( GameObject player )
	{
		if ( player == null ) return;

		var body = player.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		if ( body == null )
		{
			Log.Warning( "[Lobby_Citizen_Rules] SkinnedModelRenderer introuvable." );
			return;
		}

		// Model citizen
		body.Model = Model.Load( "models/citizen/citizen.vmdl" );

		// Dresser
		var dresser = body.Components.Get<Dresser>( FindMode.InSelf | FindMode.InChildren );
		if ( dresser == null )
			dresser = body.Components.Create<Dresser>();

		dresser.BodyTarget = body;
		dresser.Source = Dresser.ClothingSource.OwnerConnection;
		dresser.Apply();

		// Anim driver tuning (lobby)
		var anim = body.Components.Get<CitizenAnimDriver>( FindMode.EverythingInSelfAndDescendants );
		if ( anim != null )
		{
			anim.WalkRunBlendSpeed = 160f;
			anim.AimStrengthEyes = 1f;
			anim.AimStrengthHead = 1f;
			anim.AimStrengthBody = 0.2f;
		}

		Log.Info( "[Lobby_Citizen_Rules] Applied lobby citizen rules." );
	}
}
