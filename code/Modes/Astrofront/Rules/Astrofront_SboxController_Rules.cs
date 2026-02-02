using Sandbox;

namespace Astrofront;

public static class Astrofront_SboxController_Rules
{
	public static void ApplyLocal( GameObject player )
	{
		if ( player == null ) return;

		var pc = player.Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants );
		if ( pc == null )
		{
			Log.Warning( "[Astrofront_SboxController_Rules] PlayerController introuvable." );
			return;
		}

		// =========================
		// CAMERA (POV)
		// =========================

		// Si false, le PlayerController ne gère plus la caméra (utile si tu as une caméra custom).
		pc.UseCameraControls = true;

		// POV par défaut : ThirdPerson (mais le joueur peut switch via ToggleCameraModeButton).
		pc.ThirdPerson = true;

		// Bouton de toggle (par défaut c'est "view" dans s&box).
		pc.ToggleCameraModeButton = InputActions.TogglePov;

		// En FPS, cacher le corps pour éviter clipping / tête dans la caméra.
		pc.HideBodyInFirstPerson = true;

		// Utiliser le FOV des préférences joueur (settings s&box).
		pc.UseFovFromPreferences = true;

		// Décalage caméra TP (si UseCameraControls = true).
		// Attention: c'est du "feeling" - à ajuster selon ton perso.
		pc.CameraOffset = new Vector3( 256f, 0f, 12f );

		// Distance "yeux" depuis le haut du capsule/volume (surtout en FPS).
		pc.EyeDistanceFromTop = 8f;

		// =========================
		// INPUT (mouvement)
		// =========================

		// Si false, le PlayerController ne lit plus les actions move/jump/etc.
		// CorePlayerUiLock coupe ça pendant les UI modales.
		pc.UseInputControls = true;

		// Vitesse "normale" (base Astrofront).
		pc.WalkSpeed = 220f;

		// Vitesse quand on maintient le modificateur (AltMoveButton), typiquement sprint.
		pc.RunSpeed = 360f;

		// Vitesse quand accroupi.
		pc.DuckedSpeed = 120f;

		// Vitesse du saut.
		pc.JumpSpeed = 300f;

		// Accélération / décélération (0 = instantané).
		// Plus c'est haut, plus ça "glisse".
		pc.AccelerationTime   = 0.10f;
		pc.DeaccelerationTime = 0.10f;

		// Bouton modificateur de vitesse (souvent "run").
		// ⚠️ Je recommande de garder les binds stables entre modes.
		pc.AltMoveButton = "run";

		// false = sprint seulement quand on maintient "run".
		// true = "RunSpeed" par défaut et maintenir "run" fait marcher (walk).
		pc.RunByDefault = false;

		// =========================
		// SLOW WALK (3e vitesse via component Core)
		// =========================
		// Le PlayerController natif gère Walk/Run.
		// Ce component ajoute un "SlowWalk" (marche lente) en forçant temporairement WalkSpeed.
		var moveMode = player.Components.Get<SboxMovementModeController>( FindMode.EverythingInSelfAndDescendants );
		if ( moveMode != null )
		{
			// Action input (à créer dans l'éditeur)
			moveMode.SlowWalkButton = "SlowWalk";

			// Vitesse slow walk spécifique Astrofront
			moveMode.SetSlowWalkSpeed( 120f );
		}
		else
		{
			// Pas bloquant : ça veut juste dire que le prefab n'a pas le component.
			// (tu peux le rajouter sur player_core pour activer SlowWalk)
			Log.Warning( "[Astrofront_SboxController_Rules] SboxMovementModeController introuvable (SlowWalk inactif)." );
		}

		// =========================
		// USE / PRESSING (interaction)
		// =========================

		// UseButton = action utilisée pour "pressing" (interaction).
		pc.UseButton = "use";

		// Autorise le système de pressing (le controller gère le hold + callbacks).
		pc.EnablePressing = true;

		// Portée max pour l'interaction Use.
		pc.ReachLength = 130f;

		// =========================
		// LOOK
		// =========================

		// Si false, le controller ne gère plus la souris/rotation caméra.
		pc.UseLookControls = true;

		// Le perso peut se caler sur la rotation du sol (pentes).
		pc.RotateWithGround = true;

		// Clamp vertical de la vue (en degrés).
		pc.PitchClamp = 90f;

		// Sensibilité globale du look (multiplier).
		pc.LookSensitivity = 1f;

		// =========================
		// APPARENCE
		// =========================

		// Apparence citizen + wardrobe Steam
		var appearance = player.Components.Get<PlayerAppearance>( FindMode.EverythingInSelfAndDescendants );
		if ( appearance != null )
		{
			appearance.SetBodyModel( "models/citizen/citizen.vmdl" );
			appearance.SetWardrobeEnabled( true );
		}

		Log.Info( "[Astrofront_SboxController_Rules] Applied local astrofront controller settings." );
	}
}
