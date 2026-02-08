using Sandbox;

namespace Astrofront;

/// Caméra unique scalable (ThirdPerson + FirstPerson) :
/// - ThirdPerson : arc arrière + anti-clipping (trace vers la caméra)
/// - FirstPerson : ancrage tête/yeux (anchor) + rotation libre
/// - Optionnel : toggle via Input action string (sans dépendre d'une enum InputActions)
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

	/// Si true, on peut toggle le mode via un input action name (string).
	/// Exemple: "view" / "camera" / "duck" etc selon tes bindings.
	[Property, Group( "Mode" )] public bool AllowToggle { get; set; } = false;
	[Property, Group( "Mode" )] public string ToggleAction { get; set; } = "view";

	// --------------------
	// Refs
	// --------------------
	[Property, Group( "Refs" )] public GameObject Target { get; set; }              // joueur racine
	[Property, Group( "Refs" )] public GameObject FirstPersonAnchor { get; set; }  // tête/yeux (optionnel)

	// --------------------
	// Common tuning
	// --------------------
	[Property, Group( "Common" )] public float MouseSensitivity { get; set; } = 0.15f;
	[Property, Group( "Common" )] public float MinPitch { get; set; } = -10f;
	[Property, Group( "Common" )] public float MaxPitch { get; set; } = 45f;
	[Property, Group( "Common" )] public float PosLerp { get; set; } = 12f;
	[Property, Group( "Common" )] public float RotLerp { get; set; } = 16f;

	// --------------------
	// Third Person
	// --------------------
	[Property, Group( "ThirdPerson" )] public float Distance { get; set; } = 180f;           // distance caméra
	[Property, Group( "ThirdPerson" )] public float HeightOffset { get; set; } = 20f;        // focus au-dessus du joueur
	[Property, Group( "ThirdPerson" )] public float RearArcHalfAngle { get; set; } = 45f;    // demi-arc derrière le joueur (°)
	[Property, Group( "ThirdPerson" )] public bool RotateTargetWhenArcExceeded { get; set; } = true;

	/// Anti-clipping en TP : rayon de trace pour éviter de traverser les murs.
	[Property, Group( "ThirdPerson" )] public float CameraTraceRadius { get; set; } = 4f;
	[Property, Group( "ThirdPerson" )] public float CameraTracePadding { get; set; } = 2f; // décale un peu du mur

	// --------------------
	// First Person
	// --------------------
	/// Offset local appliqué depuis l'anchor FP (ex: léger recul/hauteur).
	[Property, Group( "FirstPerson" )] public Vector3 FirstPersonLocalOffset { get; set; } = Vector3.Zero;

	// --------------------
	// Internal state
	// --------------------
	private float _yawDeg;    // monde (yaw)
	private float _pitchDeg;  // pitch
	private Vector3 _goalPos;
	private Rotation _goalRot;

	private CameraComponent _cam;

	protected override void OnStart()
	{
		_cam = Components.Get<CameraComponent>( FindMode.EverythingInSelfAndDescendants );
		if ( _cam == null ) return;
		if ( Target is null ) return;

		// Init : yaw aligné avec le forward du joueur, pitch au milieu des bornes
		float frontYaw = GetYawDeg( Target.WorldRotation.Forward );
		_yawDeg = frontYaw;
		_pitchDeg = ((MinPitch + MaxPitch) * 0.5f).Clamp( MinPitch, MaxPitch );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		if ( _cam == null || !_cam.Enabled ) return;
		if ( Target is null ) return;

		// Bloque input caméra quand UI modale
		if ( UiModalController.IsUiLockedLocal ) return;

		// Toggle optionnel (string) pour ne pas dépendre d'une classe InputActions
		if ( AllowToggle && !string.IsNullOrWhiteSpace( ToggleAction ) && Input.Pressed( ToggleAction ) )
		{
			Mode = (Mode == CameraMode.ThirdPerson) ? CameraMode.FirstPerson : CameraMode.ThirdPerson;

			// Optionnel : ré-ancrer le yaw à la rotation actuelle de la cam pour éviter un "snap"
			_yawDeg = GetYawDeg( WorldRotation.Forward );
		}

		// 1) Lire la souris - INVERSION demandée :
		//    - souris vers le bas => cam MONTE  (pitch++)
		//    - souris vers la droite => cam va à DROITE du perso (yaw--)
		var md = Input.MouseDelta;
		_yawDeg -= md.x * MouseSensitivity;
		_pitchDeg += md.y * MouseSensitivity;
		_pitchDeg = _pitchDeg.Clamp( MinPitch, MaxPitch );

		// 2) Construire la rotation voulue (commune)
		_goalRot = Rotation.From( new Angles( _pitchDeg, _yawDeg, 0f ) );

		// 3) Calculer position selon mode
		if ( Mode == CameraMode.FirstPerson )
			UpdateFirstPerson();
		else
			UpdateThirdPerson();

		// 4) Smoothing
		WorldRotation = Rotation.Lerp( WorldRotation, _goalRot, RotLerp * Time.Delta );
		WorldPosition = Vector3.Lerp( WorldPosition, _goalPos, PosLerp * Time.Delta );
	}

	private void UpdateFirstPerson()
	{
		// Anchor : si non assigné -> fallback sur Target
		var anchor = FirstPersonAnchor ?? Target;

		// Pose de base = anchor
		var basePos = anchor.WorldPosition;
		var baseRot = anchor.WorldRotation;

		// On veut une rotation caméra pilotée par souris.
		// L'anchor sert surtout de point de position (et éventuellement pour un futur "headbob"/animation).
		// Offset FP en espace local de la caméra (pratique pour reculer/monter)
		var offsetWorld = _goalRot * FirstPersonLocalOffset;

		_goalPos = basePos + offsetWorld;

		// En FP : pas d'arc clamp, pas de rotate target automatique ici
		// (si tu veux tourner le perso en FP, ce sera une responsabilité du controller, pas de la caméra)
	}

	private void UpdateThirdPerson()
	{
		// Confinement arc arrière + rotation target si dépassement
		float frontYaw = GetYawDeg( Target.WorldRotation.Forward );

		float relDesired = ShortestAngleDeg( _yawDeg, frontYaw ); // [-180,180]
		float clamped = relDesired.Clamp( -RearArcHalfAngle, +RearArcHalfAngle );

		if ( RotateTargetWhenArcExceeded && !relDesired.AlmostEqual( clamped, 0.0001f ) )
		{
			float overflow = relDesired - clamped;
			float newFront = NormalizeDeg( frontYaw + overflow );

			var targetAngles = Target.WorldRotation.Angles();
			targetAngles = new Angles( 0f, newFront, 0f );
			Target.WorldRotation = Rotation.From( targetAngles );

			frontYaw = newFront;
		}

		// Yaw final confiné à l'arc
		_yawDeg = NormalizeDeg( frontYaw + clamped );

		// Recalcule rotation car yaw peut avoir été modifié par l'arc clamp
		_goalRot = Rotation.From( new Angles( _pitchDeg, _yawDeg, 0f ) );

		// Focus au-dessus du joueur
		var focus = Target.WorldPosition + Vector3.Up * HeightOffset;

		// Position voulue (derrière)
		var desiredPos = focus - _goalRot.Forward * Distance;

		// Anti-clipping : trace focus -> desiredPos
		_goalPos = ResolveThirdPersonCollision( focus, desiredPos );
	}

	private Vector3 ResolveThirdPersonCollision( Vector3 focus, Vector3 desiredPos )
	{
		// Si la trace n'a pas de sens (distance nulle)
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

		// Se placer juste avant le mur
		return tr.EndPosition + tr.Normal * CameraTracePadding;
	}

	// --------- Utils angles (degrés) ---------

	private static float NormalizeDeg( float a )
	{
		a %= 360f;
		if ( a < 0f ) a += 360f;
		return a;
	}

	private static float ShortestAngleDeg( float a, float b )
	{
		float diff = NormalizeDeg( a ) - NormalizeDeg( b );
		if ( diff > 180f ) diff -= 360f;
		if ( diff < -180f ) diff += 360f;
		return diff;
	}

	private static float GetYawDeg( Vector3 forward )
	{
		var f2 = forward.WithZ( 0 ).Normal;
		if ( f2.IsNearlyZero() ) return 0f;
		return System.MathF.Atan2( f2.y, f2.x ) * 180f / System.MathF.PI;
	}
}
