using Sandbox;
using System;

namespace Astrofront;

/// Motor de vol simple (déplacement 3D libre).
/// - Pas de gravité (par défaut)
/// - Accélération vers une vélocité cible
/// - "Air friction" pour stopper progressivement
///
/// Notes:
/// - MoveAxis = plan caméra (right/forward)
/// - Vertical : JumpHeld = monter, DuckHeld = descendre
public sealed class FlyMotor : IMovementMotor
{
	private const float VerticalSpeed = 240f;
	private const float AirFriction = 4.0f;

	// Petit kick pour "décoller" quand on est encore au sol
	private const float TakeoffImpulse = 220f;

	public void OnActivated( MovementMotorContext context ) { }
	public void OnDeactivated( MovementMotorContext context ) { }

	public void GetAnimHints( ref MovementMotorAnimHints hints )
	{
		// Tant qu'on n'a pas un vrai état "fly" dans le graph,
		// on évite l'état "falling" (bras qui tombent).
		// => On force grounded=true.
		hints.OverrideGrounded = true;
		hints.Grounded = true;

		// Laisse le mode "standard" pour l’instant.
		// (On fera un vrai move_style/special_state quand tu auras un état fly/noclip propre dans le graph)
		hints.MoveStyle = 0;
		hints.SpecialMovementStates = 0;
		hints.HoldType = 0;

		hints.AnimSpeedMultiplier = 1f;
	}

	public void Step( MovementMotorContext ctx )
	{
		var cc = ctx.Controller;
		if ( cc == null ) return;

		float dt = ctx.DeltaTime;

		// ------------------------------------
		// Axes caméra / fallback yaw
		// ------------------------------------
		Vector3 forward;
		Vector3 right;
		Vector3 upAxis = Vector3.Up;

		if ( ctx.Camera != null && ctx.Camera.Enabled )
		{
			forward = ctx.Camera.WorldRotation.Forward.Normal;

			// ✅ IMPORTANT : right = forward x up (même convention que ton WalkMotor)
			right = forward.Cross( upAxis ).Normal;

			// si caméra regarde quasi verticalement -> fallback yaw
			if ( right.IsNearlyZero() )
			{
				var yaw = ctx.Camera.WorldRotation.Forward.WithZ( 0f );
				if ( yaw.IsNearlyZero() ) yaw = Vector3.Forward;

				var basis = Rotation.LookAt( yaw.Normal, Vector3.Up );
				forward = basis.Forward;
				right = basis.Right;
			}
		}
		else
		{
			var yaw = cc.GameObject.WorldRotation.Forward.WithZ( 0f );
			if ( yaw.IsNearlyZero() ) yaw = Vector3.Forward;

			var basis = Rotation.LookAt( yaw.Normal, Vector3.Up );
			forward = basis.Forward;
			right = basis.Right;
		}

		// ------------------------------------
		// Wish velocity (plan caméra)
		// ------------------------------------
		Vector3 wishDir = (right * ctx.MoveAxis.x) + (forward * ctx.MoveAxis.y);
		if ( wishDir.LengthSquared > 1e-6f )
			wishDir = wishDir.Normal;

		float speed = ctx.DesiredSpeed * ctx.SpeedMultiplier;
		Vector3 wishVel = wishDir * speed;

		// ------------------------------------
		// Vertical
		// ------------------------------------
		float up = 0f;
		if ( ctx.JumpHeld ) up += 1f;
		if ( ctx.DuckHeld ) up -= 1f;

		// ✅ Décollage fiable : si on appuie Jump et qu'on touche le sol, on kick
		// pour casser le contact. Ensuite le maintien JumpHeld fait monter.
		if ( ctx.JumpPressed && cc.IsOnGround )
			cc.Punch( upAxis * TakeoffImpulse );

		wishVel += upAxis * (up * VerticalSpeed);

		// ------------------------------------
		// Accel vers target + air friction
		// ------------------------------------
		var vel = cc.Velocity;

		float accel = MathF.Max( 0.1f, ctx.Acceleration );
		vel = Vector3.Lerp( vel, wishVel, accel * dt );

		// Stop doux quand input relâché
		vel = vel.LerpTo( Vector3.Zero, AirFriction * dt );

		cc.Velocity = vel;
		cc.Move();
	}
}
