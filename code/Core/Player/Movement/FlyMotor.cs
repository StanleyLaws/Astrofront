using Sandbox;
using System;

namespace Astrofront;

/// Motor de vol simple (déplacement 3D libre).
/// - Pas de gravité
/// - Accélération vers une vélocité cible
/// - "Air friction" pour stopper progressivement
///
/// Notes:
/// - On utilise MoveAxis (2D) pour le plan caméra (forward/right)
/// - Pour monter/descendre, on réutilise JumpHeld / DuckHeld (temporaire, proprement extensible plus tard)
public sealed class FlyMotor : IMovementMotor
{
	// Réglages internes par défaut (peuvent être exposés plus tard via context si besoin)
	private const float VerticalSpeed = 240f;
	private const float AirFriction = 4.0f; // plus haut = stop plus vite

	public void OnActivated( MovementMotorContext context )
	{
		// Rien
	}

	public void OnDeactivated( MovementMotorContext context )
	{
		// Rien
	}

	public void Step( MovementMotorContext ctx )
	{
		var cc = ctx.Controller;
		if ( cc == null ) return;

		float dt = ctx.DeltaTime;

		// Direction basée caméra
		Vector3 fwd = cc.GameObject.WorldRotation.Forward;
		Vector3 right = fwd.Cross( Vector3.Up ).Normal;

		if ( ctx.Camera != null )
		{
			var camFwd = ctx.Camera.WorldRotation.Forward;
			var camRight = ctx.Camera.WorldRotation.Right;

			// On garde un espace caméra complet (3D), mais on stabilise un minimum
			fwd = camFwd.Normal;
			right = camRight.Normal;
		}

		// Wish velocity horizontale (dans l’espace caméra)
		Vector3 wishVel =
			(right * ctx.MoveAxis.x + fwd * ctx.MoveAxis.y);

		if ( wishVel.LengthSquared > 1e-6f )
			wishVel = wishVel.Normal;

		float speed = ctx.DesiredSpeed * ctx.SpeedMultiplier;
		wishVel *= speed;

		// Vertical (temporaire) : JumpHeld = monter, DuckHeld = descendre
		float up = 0f;
		if ( ctx.JumpHeld ) up += 1f;
		if ( ctx.DuckHeld ) up -= 1f;

		wishVel += Vector3.Up * (up * VerticalSpeed);

		// Lerp vers la vélocité cible
		var vel = cc.Velocity;

		float accel = MathF.Max( 0.1f, ctx.Acceleration );
		vel = Vector3.Lerp( vel, wishVel, accel * dt );

		// Air friction pour s’arrêter en relâchant
		vel = vel.LerpTo( Vector3.Zero, AirFriction * dt );

		cc.Velocity = vel;

		// Move
		cc.Move();
	}
}
