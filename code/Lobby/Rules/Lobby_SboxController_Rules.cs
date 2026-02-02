using Sandbox;

namespace Astrofront;

/// <summary>
/// Règles liées au PlayerController S&box pour le Lobby.
/// - POV: ThirdPerson par défaut
/// - FPS/TP autorisés
/// - Mouvement plus calme que Astrofront
/// - SlowWalk activé aussi
/// </summary>
public static class Lobby_SboxController_Rules
{
	public static void ApplyLocal( GameObject player )
	{
		if ( player == null ) return;

		var pc = player.Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants );
		if ( pc == null )
		{
			Log.Warning( "[Lobby_SboxController_Rules] PlayerController introuvable." );
			return;
		}

		// =========================
		// CAMERA
		// =========================

		pc.UseCameraControls = true;
		pc.ThirdPerson = true; // POV par défaut lobby
		pc.ToggleCameraModeButton = InputActions.TogglePov;

		pc.HideBodyInFirstPerson = true;
		pc.UseFovFromPreferences = true;
		pc.CameraOffset = new Vector3( 220f, 0f, 10f ); // un peu plus proche que Astrofront
		pc.EyeDistanceFromTop = 8f;

		// =========================
		// MOUVEMENT (plus posé que Astrofront)
		// =========================

		pc.UseInputControls = true;

		pc.WalkSpeed   = 160f; // marche normale lobby
		pc.RunSpeed    = 220f; // petit sprint
		pc.DuckedSpeed = 90f;
		pc.JumpSpeed   = 260f;

		pc.AccelerationTime   = 0.15f; // plus doux
		pc.DeaccelerationTime = 0.15f;

		pc.AltMoveButton = "run";
		pc.RunByDefault  = false;

		// =========================
		// SLOW WALK (toujours actif en lobby)
		// =========================
		var moveMode = player.Components.Get<SboxMovementModeController>( FindMode.EverythingInSelfAndDescendants );
		if ( moveMode != null )
		{
			moveMode.SlowWalkButton = "SlowWalk";
			moveMode.SetSlowWalkSpeed( 80f ); // très lent en lobby
		}
		else
		{
			Log.Warning( "[Lobby_SboxController_Rules] SboxMovementModeController introuvable (SlowWalk inactif)." );
		}

		// =========================
		// USE / INTERACTION
		// =========================

		pc.UseButton = "use";
		pc.EnablePressing = true;
		pc.ReachLength = 120f;

		// =========================
		// LOOK
		// =========================

		pc.UseLookControls = true;
		pc.RotateWithGround = true;
		pc.PitchClamp = 90f;
		pc.LookSensitivity = 1f;

		Log.Info( "[Lobby_SboxController_Rules] Applied lobby controller settings (TP default, SlowWalk enabled)." );
	}
}
