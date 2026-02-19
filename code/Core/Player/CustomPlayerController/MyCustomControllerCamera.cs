using Sandbox;

namespace Astrofront;

/// Caméra unique scalable (ThirdPerson + FirstPerson) :
/// - ThirdPerson : orbit libre autour du joueur + anti-clipping
/// - FirstPerson : anchor tête/yeux + rotation libre
/// - IMPORTANT: ApplyTransform() est fait en OnPreRender() pour gagner contre les "post-moves"
///   (héritage de parent transform / autres scripts qui écrivent après OnUpdate).
public sealed class MyCustomControllerCamera : Component
{
	// --------------------
	// Mode
	// --------------------
	public enum CameraMode
	{
		ThirdPerson = 0,
		FirstPerson = 1
	}

	[Property, Group( "Mode" )] public CameraMode Mode { get; set; } = CameraMode.ThirdPerson;
	[Property, Group( "Mode" )] public bool AllowToggle { get; set; } = false;
	[Property, Group( "Mode" )] public string ToggleAction { get; set; } = "view";

	// --------------------
	// Refs
	// --------------------
	[Property, Group( "Refs" )] public GameObject Target { get; set; }              // joueur racine
	[Property, Group( "Refs" )] public GameObject FirstPersonAnchor { get; set; }  // tête/yeux (optionnel)

	// --------------------
	// Robust
	// --------------------
	[Property, Group( "Robust" )] public bool DetachFromParentOnStart { get; set; } = false;

	// --------------------
	// Common tuning
	// --------------------
	[Property, Group( "Common" )] public float MouseSensitivity { get; set; } = 0.15f;

	// TP: smoothing agréable
	[Property, Group( "Common" )] public float ThirdPersonPosLerp { get; set; } = 12f;
	[Property, Group( "Common" )] public float ThirdPersonRotLerp { get; set; } = 16f;

	// FP: zéro inertie (par défaut)
	[Property, Group( "Common" )] public bool FirstPersonUseSmoothing { get; set; } = false;
	[Property, Group( "Common" )] public float FirstPersonPosLerp { get; set; } = 80f;
	[Property, Group( "Common" )] public float FirstPersonRotLerp { get; set; } = 120f;

	// --------------------
	// Third Person
	// --------------------
	[Property, Group( "ThirdPerson" )] public float Distance { get; set; } = 240f;
	[Property, Group( "ThirdPerson" )] public float FocusHeightOffset { get; set; } = 64f;

	[Property, Group( "ThirdPerson" )] public float ShoulderOffset { get; set; } = 18f;

	[Property, Group( "ThirdPerson" )] public float ThirdPersonMinPitch { get; set; } = -89f;
	[Property, Group( "ThirdPerson" )] public float ThirdPersonMaxPitch { get; set; } = 89f;

	[Property, Group( "ThirdPerson" )] public float MinCameraDistanceToFocus { get; set; } = 8f;

	[Property, Group( "ThirdPerson" )] public float CameraTraceRadius { get; set; } = 4f;
	[Property, Group( "ThirdPerson" )] public float CameraTracePadding { get; set; } = 2f;

	// --------------------
	// First Person
	// --------------------
	// ✅ Pitch FP séparé (plus libre que l'ancien -10/45)
	[Property, Group( "FirstPerson" )] public float FirstPersonMinPitch { get; set; } = -89f;
	[Property, Group( "FirstPerson" )] public float FirstPersonMaxPitch { get; set; } = 89f;

	// ✅ Offset FP "safe" par défaut : léger recul + légère montée
	// (ajuste dans l'inspecteur si tu veux)
	[Property, Group( "FirstPerson" )] public Vector3 FirstPersonLocalOffset { get; set; } = new Vector3( -6f, 0f, 2f );

	[Property, Group( "FirstPerson" )] public float FallbackEyeHeight { get; set; } = 64f;
	[Property, Group( "FirstPerson" )] public bool AutoFindAnchorByName { get; set; } = true;

	// --------------------
	// Debug
	// --------------------
	[Property, Group( "Debug" )] public bool DebugThirdPersonCamera { get; set; } = false;
	[Property, Group( "Debug" )] public float DebugDrawSeconds { get; set; } = 0f;

	// --------------------
	// Internal state
	// --------------------
	private float _yawDeg;
	private float _pitchDeg;
	private Vector3 _goalPos;
	private Rotation _goalRot;

	private CameraComponent _cam;

	private Vector3 _lastAppliedPos;
	private Rotation _lastAppliedRot;
	private bool _hasLastApplied;

	protected override void OnStart()
	{
		_cam = Components.Get<CameraComponent>( FindMode.EverythingInSelfAndDescendants );
		if ( _cam == null ) return;
		if ( Target is null ) return;

		if ( DetachFromParentOnStart && GameObject.Parent != null )
		{
			GameObject.SetParent( null, keepWorldPosition: true );
		}

		if ( FirstPersonAnchor == null && AutoFindAnchorByName )
			FirstPersonAnchor = TryFindFirstPersonAnchor( Target );

		float frontYaw = GetYawDeg( Target.WorldRotation.Forward );
		_yawDeg = frontYaw;

		// ✅ init pitch selon mode
		if ( Mode == CameraMode.FirstPerson )
			_pitchDeg = ((FirstPersonMinPitch + FirstPersonMaxPitch) * 0.5f).Clamp( FirstPersonMinPitch, FirstPersonMaxPitch );
		else
			_pitchDeg = ((ThirdPersonMinPitch + ThirdPersonMaxPitch) * 0.5f).Clamp( ThirdPersonMinPitch, ThirdPersonMaxPitch );

		_goalRot = Rotation.From( new Angles( _pitchDeg, _yawDeg, 0f ) );
		_goalPos = WorldPosition;

		_lastAppliedPos = WorldPosition;
		_lastAppliedRot = WorldRotation;
		_hasLastApplied = true;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		if ( _cam == null || !_cam.Enabled ) return;
		if ( Target is null ) return;

		if ( UiModalController.IsUiLockedLocal ) return;

		if ( DebugThirdPersonCamera && _hasLastApplied )
		{
			var dp = (WorldPosition - _lastAppliedPos).Length;
			var dy = Angles.NormalizeAngle( WorldRotation.Angles().yaw - _lastAppliedRot.Angles().yaw );

			if ( dp > 0.25f || System.MathF.Abs( dy ) > 0.25f )
			{
				DebugOverlay.ScreenText(
					new Vector2( 40, 80 ),
					$"[Cam Debug] External move since last apply: dp={dp:F2} dyaw={dy:F2}",
					16f, TextFlag.LeftTop, Color.Orange, 0f
				);
			}
		}

		if ( AllowToggle && !string.IsNullOrWhiteSpace( ToggleAction ) && Input.Pressed( ToggleAction ) )
		{
			Mode = (Mode == CameraMode.ThirdPerson) ? CameraMode.FirstPerson : CameraMode.ThirdPerson;
			RecenterFromCurrentCameraRotation();

			if ( Mode == CameraMode.FirstPerson && FirstPersonAnchor == null && AutoFindAnchorByName )
				FirstPersonAnchor = TryFindFirstPersonAnchor( Target );
		}

		// Input souris
		var md = Input.MouseDelta;
		_yawDeg -= md.x * MouseSensitivity;
		_pitchDeg += md.y * MouseSensitivity;

		// ✅ Clamp pitch selon mode (FP séparé)
		if ( Mode == CameraMode.ThirdPerson )
			_pitchDeg = _pitchDeg.Clamp( ThirdPersonMinPitch, ThirdPersonMaxPitch );
		else
			_pitchDeg = _pitchDeg.Clamp( FirstPersonMinPitch, FirstPersonMaxPitch );

		_goalRot = Rotation.From( new Angles( _pitchDeg, _yawDeg, 0f ) );

		if ( Mode == CameraMode.FirstPerson )
			UpdateFirstPersonGoals();
		else
			UpdateThirdPersonGoals();
	}

	protected override void OnPreRender()
	{
		if ( IsProxy ) return;
		if ( _cam == null || !_cam.Enabled ) return;
		if ( Target is null ) return;

		ApplyTransform();

		_lastAppliedPos = WorldPosition;
		_lastAppliedRot = WorldRotation;
		_hasLastApplied = true;
	}

	private void ApplyTransform()
	{
		if ( Mode == CameraMode.FirstPerson )
		{
			if ( !FirstPersonUseSmoothing )
			{
				WorldRotation = _goalRot;
				WorldPosition = _goalPos;
				return;
			}

			WorldRotation = Rotation.Lerp( WorldRotation, _goalRot, FirstPersonRotLerp * Time.Delta );
			WorldPosition = Vector3.Lerp( WorldPosition, _goalPos, FirstPersonPosLerp * Time.Delta );
			return;
		}

		WorldRotation = Rotation.Lerp( WorldRotation, _goalRot, ThirdPersonRotLerp * Time.Delta );
		WorldPosition = Vector3.Lerp( WorldPosition, _goalPos, ThirdPersonPosLerp * Time.Delta );
	}

	private void UpdateFirstPersonGoals()
	{
		if ( FirstPersonAnchor != null )
		{
			var basePos = FirstPersonAnchor.WorldPosition;
			var offsetWorld = _goalRot * FirstPersonLocalOffset;
			_goalPos = basePos + offsetWorld;
			return;
		}

		var fallbackPos = Target.WorldPosition + Vector3.Up * FallbackEyeHeight;
		var fallbackOffset = _goalRot * FirstPersonLocalOffset;
		_goalPos = fallbackPos + fallbackOffset;
	}

	private void UpdateThirdPersonGoals()
	{
		var focus = Target.WorldPosition + Vector3.Up * FocusHeightOffset;

		var desiredPos = focus - _goalRot.Forward * Distance;

		if ( ShoulderOffset != 0f )
			desiredPos += _goalRot.Right * ShoulderOffset;

		var toCam = desiredPos - focus;
		var len = toCam.Length;
		if ( len < MinCameraDistanceToFocus )
			desiredPos = focus + toCam.Normal * MinCameraDistanceToFocus;

		var resolved = ResolveThirdPersonCollision( focus, desiredPos );

		if ( DebugThirdPersonCamera )
			DrawThirdPersonDebug( focus, desiredPos, resolved );

		_goalPos = resolved;
	}

	private Vector3 ResolveThirdPersonCollision( Vector3 focus, Vector3 desiredPos )
	{
		if ( (desiredPos - focus).LengthSquared < 0.001f )
			return desiredPos;

		var tr = Scene.Trace
			.Ray( focus, desiredPos )
			.Radius( CameraTraceRadius )
			.IgnoreGameObject( Target )
			.IgnoreGameObject( GameObject )
			.Run();

		if ( !tr.Hit )
			return desiredPos;

		return tr.EndPosition + tr.Normal * CameraTracePadding;
	}

	private void DrawThirdPersonDebug( Vector3 focus, Vector3 desiredPos, Vector3 resolvedPos )
	{
		float dur = DebugDrawSeconds;

		DebugOverlay.Sphere( new Sphere( focus, 2.5f ), Color.Green, dur );
		DebugOverlay.Sphere( new Sphere( resolvedPos, 2.0f ), Color.Red, dur );
		DebugOverlay.Sphere( new Sphere( desiredPos, 3.0f ), Color.Yellow, dur );

		var tr = Scene.Trace
			.Ray( focus, desiredPos )
			.Radius( CameraTraceRadius )
			.IgnoreGameObject( Target )
			.IgnoreGameObject( GameObject )
			.Run();

		DebugOverlay.Trace( tr, dur );
		if ( tr.Hit )
			DebugOverlay.Normal( tr.EndPosition, tr.Normal * 12f, Color.Cyan, dur );

		var parentName = GameObject.Parent != null ? GameObject.Parent.Name : "<null>";

		DebugOverlay.ScreenText(
			new Vector2( 40, 40 ),
			$"[TP Cam Debug] parent={parentName} hit={tr.Hit} shoulder={ShoulderOffset:F1} mode={Mode}",
			16f, TextFlag.LeftTop, Color.White, 0f
		);
	}

	private void RecenterFromCurrentCameraRotation()
	{
		var a = WorldRotation.Angles();
		_pitchDeg = a.pitch;
		_yawDeg = NormalizeDeg( a.yaw );
		_goalRot = Rotation.From( new Angles( _pitchDeg, _yawDeg, 0f ) );
	}

	private static GameObject TryFindFirstPersonAnchor( GameObject root )
	{
		if ( root == null ) return null;

		return FindChildByNameRecursive( root, "Eyes" )
			?? FindChildByNameRecursive( root, "Eye" )
			?? FindChildByNameRecursive( root, "Head" )
			?? FindChildByNameRecursive( root, "Camera" );
	}

	private static GameObject FindChildByNameRecursive( GameObject root, string name )
	{
		if ( root == null ) return null;

		if ( root.Name.Equals( name, System.StringComparison.OrdinalIgnoreCase ) )
			return root;

		foreach ( var child in root.Children )
		{
			var found = FindChildByNameRecursive( child, name );
			if ( found != null ) return found;
		}

		return null;
	}

	private static float NormalizeDeg( float a )
	{
		a %= 360f;
		if ( a < 0f ) a += 360f;
		return a;
	}

	private static float GetYawDeg( Vector3 forward )
	{
		var f2 = forward.WithZ( 0 ).Normal;
		if ( f2.IsNearlyZero() ) return 0f;
		return System.MathF.Atan2( f2.y, f2.x ) * 180f / System.MathF.PI;
	}
}
