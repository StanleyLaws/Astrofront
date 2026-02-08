using Sandbox;
using System;

namespace Astrofront;

/// Motor de locomotion au sol (walk / run / sprint).
/// Gère :
/// - accélération horizontale
/// - friction sol
/// - gravité
/// - saut avec coyote + buffer
/// Ne gère PAS : stamina, slowwalk rules, énergie, UI, etc.
public sealed class WalkMotor : IMovementMotor
{
	private float _coyoteUntil;
	private float _jumpBufferUntil = float.NegativeInfinity;

	public void OnActivated( MovementMotorContext context )
	{
		_coyoteUntil = 0f;
		_jumpBufferUntil = float.NegativeInfinity;
	}

	public void OnDeactivated( MovementMotorContext context )
	{
	}

	public void Step( MovementMotorContext ctx )
	{
		var cc = ctx.Controller;
		if ( cc == null ) return;

		float dt = ctx.DeltaTime;

		// -----------------------
		// Jump buffer + coyote
		// -----------------------
		if ( ctx.JumpPressed )
			_jumpBufferUntil = Time.Now + ctx.JumpBuffer;

		if ( cc.IsOnGround )
			_coyoteUntil = Time.Now + ctx.CoyoteTime;

		bool buffered = Time.Now <= _jumpBufferUntil;
		bool canJump = cc.IsOnGround || Time.Now <= _coyoteUntil;

		if ( buffered && canJump )
		{
			_jumpBufferUntil = float.NegativeInfinity;
			_coyoteUntil = 0f;
			cc.Punch( Vector3.Up * ctx.JumpSpeed );
		}

		// -----------------------
		// Gravité
		// -----------------------
		var vel = cc.Velocity;
		vel = vel.WithZ( vel.z - ctx.Gravity * dt );
		cc.Velocity = vel;

		// -----------------------
		// Friction au sol
		// -----------------------
		if ( cc.IsOnGround )
			cc.ApplyFriction( ctx.Friction, ctx.StopSpeed );

		// ----------------------- 
		// Accélération horizontale
		// -----------------------
		Vector3 wishDir = Vector3.Zero;

		if ( ctx.Camera != null )
		{
			var camFwdFlat = ctx.Camera.WorldRotation.Forward.WithZ( 0 );
			if ( !camFwdFlat.IsNearlyZero() )
			{
				var basis = Rotation.LookAt( camFwdFlat.Normal, Vector3.Up );
				var fwd = basis.Forward;
				var right = fwd.Cross( Vector3.Up ).Normal;

				wishDir = right * ctx.MoveAxis.x + fwd * ctx.MoveAxis.y;
				if ( wishDir.LengthSquared > 1e-6f )
					wishDir = wishDir.Normal;
			}
		}

		float speed = ctx.DesiredSpeed * ctx.SpeedMultiplier;

		cc.Accelerate( wishDir * speed );

		// -----------------------
		// Move final
		// -----------------------
		cc.Move();
	}
}
