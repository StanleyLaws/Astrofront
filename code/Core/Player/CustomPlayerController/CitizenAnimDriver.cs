using Sandbox;
using System;

namespace Astrofront;

/// Pilote citizen.vanmgrph à partir de MyCustomController + Camera.
/// Ne gère QUE l'animation (pas la physique).
///
/// Robust:
/// - Force l'override d'AnimationGraph (certains composants peuvent reset le renderer)
/// - Ré-applique au début + fin d'OnUpdate
///
/// Turn-in-place:
/// - Alimente move_rotationspeed (deg/sec) à partir du yaw du Body.
/*
 FUTURE IMPROVEMENT — Animation Snapshot Networking
 --------------------------------------------------
 Actuellement :
 - Le joueur local calcule toute son animation (wish_*, aim_*, duck, move_rotationspeed).
 - Les autres joueurs voient l’animation principalement via la vélocité réseau + aimDir sync.

 C’est suffisant pour :
 ✔ serveurs listen
 ✔ serveurs dédiés
 ✔ gameplay standard

 À envisager plus tard (FPS compétitif / animations très précises en réseau) :
 Mettre en place un "Animation Snapshot" envoyé Owner → Host → Clients contenant :
   - wishVelocity (Vector2)
   - duckAmount (float)
   - grounded (bool)
   - yawRotationSpeed (float)
   - aimDirection (Vector3)

 Objectif :
 - Éviter que les proxies déduisent uniquement l’anim depuis la physique
 - Améliorer la fidélité des strafes, starts/stops, et transitions rapides

 Important :
 Ce snapshot ne remplace PAS la physique réseau, il ne sert qu’à l’animation visuelle.
*/


public sealed class CitizenAnimDriver : Component
{
	[Property, Group("Refs")] public MyCustomController Controller { get; set; }
	[Property, Group("Refs")] public SkinnedModelRenderer Body { get; set; }
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
		ForceApplyGraph();

		_lastPos = Controller != null ? Controller.WorldPosition : WorldPosition;
		_lastDuck = Controller != null ? Controller.DuckAmount : 0f;

		_lastYawDeg = GetYawDeg( GameObject.WorldRotation );
		_smoothedYawSpeed = 0f;
	}

	protected override void OnUpdate()
	{
		ResolveRefs();
		if ( Body == null ) return;

		// Robust: protect against other components resetting the renderer/animgraph.
		ForceApplyGraph();

		if ( !Body.UseAnimGraph || Body.AnimationGraph == null )
			return;

		bool isLocalOwner = (Controller?.Network?.Owner != null && Controller.Network.Owner == Connection.Local);

		// --- Turn-in-place: move_rotationspeed (deg/sec) ---
		// On calcule la vitesse de yaw du body indépendamment du mouvement.
		// Le graph s'en sert pour l'anim "pivot sur place".
		float yawDeg = GetYawDeg( GameObject.WorldRotation );
		float deltaYaw = Angles.NormalizeAngle( yawDeg - _lastYawDeg );
		_lastYawDeg = yawDeg;

		float rawYawSpeed = (Time.Delta > 0f) ? (deltaYaw / Time.Delta) : 0f;
		rawYawSpeed = rawYawSpeed.Clamp( -TurnSpeedClamp, TurnSpeedClamp );

		_smoothedYawSpeed = _smoothedYawSpeed.LerpTo( rawYawSpeed, TurnSpeedSmooth * Time.Delta );

		// --- Duck ---
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

		// --- Intentions (wish_*) ---
		float wish_x = 0f, wish_y = 0f, wish_speed = 0f, wish_direction = 0f;
		bool hasMoveInput = false;

		if ( isLocalOwner && !UiModalController.IsUiLockedLocal )
		{
			var mv = Input.AnalogMove;
			float moveForward = mv.x;
			float moveRight = -mv.y;

			var wish2 = new Vector2( moveRight, moveForward );
			float mag = wish2.Length.Clamp( 0f, 1f );

			wish_x = ( moveForward * WalkRunBlendSpeed ) * duckSpeedMul;
			wish_y = ( moveRight   * WalkRunBlendSpeed ) * duckSpeedMul;
			wish_speed = mag * WalkRunBlendSpeed * duckSpeedMul;
			hasMoveInput = mag > 0.01f;

			if ( mag > 0.001f )
				wish_direction = MathF.Atan2( wish_y, wish_x ) * 180f / MathF.PI;
		}

		// --- Mouvement réel (move_*) ---
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

		// UI lock: idle propre côté owner
		if ( isLocalOwner && UiModalController.IsUiLockedLocal )
		{
			wish_x = wish_y = wish_speed = 0f;
			hasMoveInput = false;
			move_x = move_y = move_spd = 0f;

			// On peut garder le turn-in-place même en UI lock si tu veux.
			// Là on le garde (ça évite de figer en tournant la caméra).
		}

		bool grounded = ProbeGround( pos, Controller != null ? Controller.Radius : 16f );

		// --- Apply params ---
		Body.Set( "duck", duckVal );
		Body.Set( "b_ducking", duckVal > 0.5f );

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

		Body.Set( "aim_body", aimDir );
		Body.Set( "aim_head", aimDir );
		Body.Set( "aim_eyes", aimDir );
		Body.Set( "aim_body_weight", AimStrengthBody );
		Body.Set( "aim_head_weight", AimStrengthHead );
		Body.Set( "aim_eyes_weight", AimStrengthEyes );

		// Intentions / movement
		Body.Set( "wish_x", wish_x );
		Body.Set( "wish_y", wish_y );
		Body.Set( "wish_speed", wish_speed );
		Body.Set( "wish_groundspeed", wish_speed );
		Body.Set( "wish_direction", wish_direction );
		Body.Set( "has_move_input", hasMoveInput );

		Body.Set( "move_x", move_x );
		Body.Set( "move_y", move_y );
		Body.Set( "move_speed", move_spd );
		Body.Set( "move_groundspeed", move_spd );
		Body.Set( "move_direction", move_direction );

		// ✅ Turn-in-place input
		Body.Set( "move_rotationspeed", _smoothedYawSpeed );

		Body.Set( "moving", (move_spd > 1.0f) || (justExitedDuck && hasMoveInput) );
		Body.Set( "b_grounded", grounded );
		Body.Set( "b_grouned", grounded );

		// Re-apply again at end of frame (extra safety vs external resets).
		ForceApplyGraph();
	}

	private void ResolveRefs()
	{
		Body ??= Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		Controller ??= Components.Get<MyCustomController>( FindMode.EverythingInSelfAndAncestors );
		if ( Camera is null && Controller != null ) Camera = Controller.Camera;
	}

	private void ForceApplyGraph()
	{
		if ( Body == null ) return;

		if ( _loadedGraph == null && !string.IsNullOrEmpty( GraphPath ) )
			_loadedGraph = AnimationGraph.Load( GraphPath );

		if ( _loadedGraph == null || _loadedGraph.IsError )
			return;

		Body.UseAnimGraph = true;

		if ( Body.AnimationGraph != _loadedGraph )
			Body.AnimationGraph = _loadedGraph;
	}

	private static float GetYawDeg( Rotation rot )
	{
		// On force yaw sur le plan XY (Z-up).
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
