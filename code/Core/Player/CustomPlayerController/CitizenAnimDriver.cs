using Sandbox;
using System;

namespace Astrofront;

/// Pilote citizen.vanmgrph à partir de MyCustomController + Camera.
/// Ne gère QUE l'animation (pas la physique).
///
/// Multi-target:
/// - Body : world model (renderer principal)
/// - Legs : renderer additionnel (ex: legs-only local FPS) recevant les mêmes paramètres
///
/// Robust:
/// - Force l'override d'AnimationGraph (certains composants peuvent reset le renderer)
/// - Ré-applique au début + fin d'OnUpdate
///
/// Turn-in-place:
/// - Alimente move_rotationspeed (deg/sec) à partir du yaw du Body (fallback: GameObject)
public sealed class CitizenAnimDriver : Component
{
	[Property, Group("Refs")] public MyCustomController Controller { get; set; }
	[Property, Group("Refs")] public SkinnedModelRenderer Body { get; set; }

	/// Renderer optionnel (ex: legs-only) recevant les mêmes paramètres que Body.
	[Property, Group("Refs")] public SkinnedModelRenderer Legs { get; set; }

	[Property, Group("Refs")] public CameraComponent Camera { get; set; }

	[Property, Group("Graph")] public string GraphPath { get; set; } = "models/citizen/citizen.vanmgrph";

	[Property, Group("Tuning")] public float WalkRunBlendSpeed { get; set; } = 220f;

	[Property, Group("Aim")] public float AimStrengthEyes { get; set; } = 1f;
	[Property, Group("Aim")] public float AimStrengthHead { get; set; } = 1f;
	[Property, Group("Aim")] public float AimStrengthBody { get; set; } = 0.2f;
	[Property, Group("Aim")] public bool AimInLocalSpace { get; set; } = true;

	[Property, Group("Turn")] public float TurnSpeedSmooth { get; set; } = 12f; // smoothing
	[Property, Group("Turn")] public float TurnSpeedClamp { get; set; } = 720f; // deg/sec clamp

	private Vector3 _lastPos;

	[Sync] private Vector3 _syncedAimDir { get; set; } = Vector3.Forward;
	private RealTimeSince _timeSinceAimSent;

	private float _lastDuck;

	private AnimationGraph _loadedGraph;

	// Turn-in-place helpers
	private float _lastYawDeg;
	private float _smoothedYawSpeed;

	protected override void OnStart()
	{
		ResolveRefs();
		ForceApplyGraph( Body );
		ForceApplyGraph( Legs );

		_lastPos = Controller != null ? Controller.WorldPosition : WorldPosition;
		_lastDuck = Controller != null ? Controller.DuckAmount : 0f;

		_lastYawDeg = GetYawDeg( GetYawSourceRotation() );
		_smoothedYawSpeed = 0f;
	}

	protected override void OnUpdate()
	{
		ResolveRefs();

		// Si aucun renderer, rien à faire
		if ( Body == null && Legs == null ) return;

		// Graphs (robuste)
		ForceApplyGraph( Body );
		ForceApplyGraph( Legs );

		// Si on a un renderer, il doit être utilisable. (legs peut être null)
		if ( !HasValidAnimGraph( Body ) && !HasValidAnimGraph( Legs ) )
			return;

		// Owner local = calcule wish_* depuis MovementInput + envoie aim via RPC
		bool isLocalOwner = (Controller?.Network?.Owner != null && Controller.Network.Owner == Connection.Local);

		// --------------------------------------------------------
		// Anim Hints (depuis le motor actif via MyCustomController)
		// --------------------------------------------------------
		var hints = Controller != null ? Controller.LastAnimHints : MovementMotorAnimHints.Default;

		// --------------------------------------------------------
		// Turn-in-place: move_rotationspeed (deg/sec)
		// --------------------------------------------------------
		float yawDeg = GetYawDeg( GetYawSourceRotation() );
		float deltaYaw = Angles.NormalizeAngle( yawDeg - _lastYawDeg );
		_lastYawDeg = yawDeg;

		float rawYawSpeed = (Time.Delta > 0f) ? (deltaYaw / Time.Delta) : 0f;
		rawYawSpeed = rawYawSpeed.Clamp( -TurnSpeedClamp, TurnSpeedClamp );
		_smoothedYawSpeed = _smoothedYawSpeed.LerpTo( rawYawSpeed, TurnSpeedSmooth * Time.Delta );

		// --------------------------------------------------------
		// Duck
		// --------------------------------------------------------
		float duckVal = (Controller != null) ? Controller.DuckAmount : 0f;
		bool justExitedDuck = false;

		if ( MathF.Abs( duckVal - _lastDuck ) > 0.02f )
		{
			if ( _lastDuck >= 0.5f && duckVal < 0.5f )
				justExitedDuck = true;

			_lastDuck = duckVal;
		}

		float duckSpeedMul = 1f;
		if ( Controller != null )
			duckSpeedMul = MathX.Lerp( 1f, Controller.DuckSpeedMultiplier, duckVal );

		// --------------------------------------------------------
		// Intentions (wish_*) : uniquement côté owner
		// IMPORTANT: utiliser MovementInput (pas Input.* direct)
		// --------------------------------------------------------
		float wish_x = 0f, wish_y = 0f, wish_speed = 0f, wish_direction = 0f;
		bool hasMoveInput = false;

		if ( isLocalOwner && !UiModalController.IsUiLockedLocal )
		{
			var move2 = Controller?.MovementInput?.MoveAxis ?? Vector2.Zero; // x=right, y=forward

			float mag = move2.Length.Clamp( 0f, 1f );
			hasMoveInput = mag > 0.01f;

			// citizen.vanmgrph : wish_x=forward, wish_y=right
			float animMul = (hints.AnimSpeedMultiplier <= 0f) ? 1f : hints.AnimSpeedMultiplier;
			float mul = duckSpeedMul * animMul;

			wish_x = ( move2.y * WalkRunBlendSpeed ) * mul;
			wish_y = ( move2.x * WalkRunBlendSpeed ) * mul;
			wish_speed = mag * WalkRunBlendSpeed * mul;

			if ( mag > 0.001f )
				wish_direction = MathF.Atan2( wish_y, wish_x ) * 180f / MathF.PI;
		}

		// --------------------------------------------------------
		// Mouvement réel (move_*) : basé sur vitesse physique (pas *animMul*)
		// --------------------------------------------------------
		var pos = Controller != null ? Controller.WorldPosition : WorldPosition;
		var velFromPos = (pos - _lastPos) / Time.Delta;
		_lastPos = pos;

		var vel = Controller != null ? Controller.Velocity : velFromPos;

		var fwd = GameObject.WorldRotation.Forward.WithZ( 0f ).Normal;
		var rightAxis = (fwd.Cross( Vector3.Up )).Normal;

		var velFlat = vel.WithZ( 0f );
		float fwdSpd = velFlat.Dot( fwd );
		float sideSpd = velFlat.Dot( rightAxis );

		float move_x = fwdSpd;
		float move_y = sideSpd;
		float move_spd = MathF.Sqrt( fwdSpd * fwdSpd + sideSpd * sideSpd );
		float move_direction = (move_spd > 0.01f) ? MathF.Atan2( move_y, move_x ) * 180f / MathF.PI : 0f;

		if ( isLocalOwner && UiModalController.IsUiLockedLocal )
		{
			wish_x = wish_y = wish_speed = 0f;
			hasMoveInput = false;
			move_x = move_y = move_spd = 0f;
		}

		// Grounded : probe par défaut, override possible (fly/zeroG)
		bool grounded = ProbeGround( pos, Controller != null ? Controller.Radius : 16f );
		if ( hints.OverrideGrounded )
			grounded = hints.Grounded;

		// AIM
		Vector3 aimDir;
		if ( isLocalOwner && Camera != null && Camera.Enabled )
		{
			var camFwd = Camera.WorldRotation.Forward.Normal;
			aimDir = AimInLocalSpace ? (GameObject.WorldRotation.Inverse * camFwd).Normal : camFwd;

			if ( _timeSinceAimSent > 0.1f && _syncedAimDir.Distance( aimDir ) > 0.01f )
			{
				_timeSinceAimSent = 0;
				RpcSyncAim( aimDir );
			}
		}
		else
		{
			aimDir = _syncedAimDir;
		}

		// --------------------------------------------------------
		// Apply params au(x) renderer(s)
		// --------------------------------------------------------
		ApplyAllParams( Body,
			hints, duckVal, hasMoveInput, grounded,
			aimDir,
			wish_x, wish_y, wish_speed, wish_direction,
			move_x, move_y, move_spd, move_direction,
			_smoothedYawSpeed,
			justExitedDuck );

		ApplyAllParams( Legs,
			hints, duckVal, hasMoveInput, grounded,
			aimDir,
			wish_x, wish_y, wish_speed, wish_direction,
			move_x, move_y, move_spd, move_direction,
			_smoothedYawSpeed,
			justExitedDuck );

		// Ré-apply robuste (certains composants reset)
		ForceApplyGraph( Body );
		ForceApplyGraph( Legs );
	}

	private void ApplyAllParams(
		SkinnedModelRenderer r,
		MovementMotorAnimHints hints,
		float duckVal,
		bool hasMoveInput,
		bool grounded,
		Vector3 aimDir,
		float wish_x, float wish_y, float wish_speed, float wish_direction,
		float move_x, float move_y, float move_spd, float move_direction,
		float moveRotationSpeed,
		bool justExitedDuck )
	{
		if ( r == null ) return;
		if ( !HasValidAnimGraph( r ) ) return;

		r.Set( "move_style", hints.MoveStyle );
		r.Set( "special_movement_states", hints.SpecialMovementStates );
		r.Set( "holdtype", hints.HoldType );

		if ( hints.ForceFirstPersonFlag )
			r.Set( "b_firstperson", hints.FirstPersonFlag );

		r.Set( "duck", duckVal );
		r.Set( "b_ducking", duckVal > 0.5f );

		// AIM
		r.Set( "aim_body", aimDir );
		r.Set( "aim_head", aimDir );
		r.Set( "aim_eyes", aimDir );
		r.Set( "aim_body_weight", AimStrengthBody );
		r.Set( "aim_head_weight", AimStrengthHead );
		r.Set( "aim_eyes_weight", AimStrengthEyes );

		// Intentions / movement
		r.Set( "wish_x", wish_x );
		r.Set( "wish_y", wish_y );
		r.Set( "wish_speed", wish_speed );
		r.Set( "wish_groundspeed", wish_speed );
		r.Set( "wish_direction", wish_direction );
		r.Set( "has_move_input", hasMoveInput );

		r.Set( "move_x", move_x );
		r.Set( "move_y", move_y );
		r.Set( "move_speed", move_spd );
		r.Set( "move_groundspeed", move_spd );
		r.Set( "move_direction", move_direction );

		r.Set( "move_rotationspeed", moveRotationSpeed );

		bool moving = (move_spd > 1.0f) || (justExitedDuck && hasMoveInput);
		if ( hints.OverrideMoving )
			moving = hints.Moving;

		r.Set( "moving", moving );
		r.Set( "b_grounded", grounded );

		// Back-compat typo déjà présent
		r.Set( "b_grouned", grounded );
	}

	private void ResolveRefs()
	{
		// Body: même logique qu'avant
		Body ??= Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		Controller ??= Components.Get<MyCustomController>( FindMode.EverythingInSelfAndAncestors );

		// Camera: récup depuis controller
		if ( Camera is null && Controller != null ) Camera = Controller.Camera;

		// Legs: on tente une résolution automatique non-invasive :
		// - d'abord via propriété (si déjà assignée, on ne touche pas)
		// - sinon, on cherche un renderer explicitement nommé "Legs" dans les descendants
		if ( Legs == null )
		{
			foreach ( var r in Components.GetAll<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
			{
				if ( r == null ) continue;
				if ( r == Body ) continue;

				if ( string.Equals( r.GameObject?.Name, "Legs", StringComparison.OrdinalIgnoreCase ) )
				{
					Legs = r;
					break;
				}
			}
		}
	}

	private void ForceApplyGraph( SkinnedModelRenderer r )
	{
		if ( r == null ) return;

		if ( _loadedGraph == null && !string.IsNullOrEmpty( GraphPath ) )
			_loadedGraph = AnimationGraph.Load( GraphPath );

		if ( _loadedGraph == null || _loadedGraph.IsError )
			return;

		r.UseAnimGraph = true;

		if ( r.AnimationGraph != _loadedGraph )
			r.AnimationGraph = _loadedGraph;
	}

	private bool HasValidAnimGraph( SkinnedModelRenderer r )
	{
		if ( r == null ) return false;
		if ( !r.UseAnimGraph ) return false;
		return r.AnimationGraph != null;
	}

	private Rotation GetYawSourceRotation()
	{
		// Turn-in-place: idéalement basé sur le body si présent (car le body peut être décalé dans la hiérarchie)
		if ( Body != null ) return Body.WorldRotation;
		return GameObject.WorldRotation;
	}

	private static float GetYawDeg( Rotation rot )
	{
		var f = rot.Forward.WithZ( 0f );
		if ( f.IsNearlyZero() ) return 0f;
		f = f.Normal;
		return MathF.Atan2( f.y, f.x ) * 180f / MathF.PI;
	}

	[Rpc.Broadcast]
	private void RpcSyncAim( Vector3 dir )
	{
		_syncedAimDir = dir.Normal;
	}

	private bool ProbeGround( Vector3 pos, float radius )
	{
		var tr = Scene.Trace
			.Ray( pos + Vector3.Up * 1f, pos + Vector3.Down * 6f )
			.Radius( radius * 0.95f )
			.IgnoreGameObject( GameObject )
			.Run();

		return tr.Hit;
	}
}
