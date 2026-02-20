using Sandbox;

namespace Astrofront;

/// Caméra unique scalable (ThirdPerson + FirstPerson) :
/// - ThirdPerson : orbit libre autour du joueur + anti-clipping
/// - FirstPerson : anchor tête/yeux + rotation libre
/// - IMPORTANT: ApplyTransform() est fait en OnPreRender() pour gagner contre les "post-moves"
///   (héritage de parent transform / autres scripts qui écrivent après OnUpdate).
///
/// TP Rear Arc (turn-in-place via caméra):
/// - La caméra peut orbiter librement dans un cône arrière (±ThirdPersonRearArcHalfAngle autour du yaw du perso)
/// - Si l'input souris dépasse l'arc, la caméra reste au bord de l'arc et l'excès est reporté sur OrientationRoot
///   => turn-in-place garanti, indépendant de la vitesse de souris.
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

	/// Root à tourner en TP quand on dépasse l'arc (turn-in-place).
	/// - Par défaut: Target
	/// - Recommandé: le GO visuel (ex: "Body"/"Citizen"/"Visual") si tu ne veux pas tourner le root physique.
	[Property, Group( "Refs" )] public GameObject OrientationRoot { get; set; }

	/// ✅ Permissions FP/TP (pilotées par Rules via PlayerUiContext)
	[Property, Group( "Refs" )] public PlayerUiContext UiContext { get; set; }

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

	/// Arc arrière (demi-angle, degrés). 60 = cône serré, 90 = hémisphère arrière.
	[Property, Group( "ThirdPerson" )] public float ThirdPersonRearArcHalfAngle { get; set; } = 60f;

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
	// Internal state
	// --------------------
	private float _yawDeg;
	private float _pitchDeg;
	private Vector3 _goalPos;
	private Rotation _goalRot;

	private CameraComponent _cam;

	private Vector3 _lastAppliedPos;
	private Rotation _lastAppliedRot;

	protected override void OnStart()
	{
		_cam = Components.Get<CameraComponent>( FindMode.EverythingInSelfAndDescendants );
		if ( _cam == null ) return;
		if ( Target is null ) return;

		OrientationRoot ??= Target;

		if ( DetachFromParentOnStart && GameObject.Parent != null )
		{
			GameObject.SetParent( null, keepWorldPosition: true );
		}

		ResolveUiContext();
		EnforceViewPermissions(); // ✅ applique les permissions dès le start

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
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		if ( _cam == null || !_cam.Enabled ) return;
		if ( Target is null ) return;

		if ( UiModalController.IsUiLockedLocal ) return;

		ResolveUiContext();
		EnforceViewPermissions(); // ✅ force FP/TP selon rules

		bool canToggle = AllowToggle && CanToggleByRules();

		if ( canToggle && !string.IsNullOrWhiteSpace( ToggleAction ) && Input.Pressed( ToggleAction ) )
		{
			Mode = (Mode == CameraMode.ThirdPerson) ? CameraMode.FirstPerson : CameraMode.ThirdPerson;

			// si on a toggle vers une vue interdite (au cas où), on corrige
			EnforceViewPermissions();

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

		// ✅ TP : arc arrière + report du dépassement sur le perso (turn-in-place)
		if ( Mode == CameraMode.ThirdPerson )
		{
			ApplyThirdPersonRearArcAndTurnInPlace();
		}

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

	private void ApplyThirdPersonRearArcAndTurnInPlace()
	{
		var root = OrientationRoot ?? Target;
		if ( root == null ) return;

		float max = ThirdPersonRearArcHalfAngle.Clamp( 0f, 180f );

		// Si max >= 180 : pas de limite
		if ( max >= 179.999f )
			return;

		float bodyYaw = NormalizeDeg( root.WorldRotation.Angles().yaw );
		float camYaw = NormalizeDeg( _yawDeg );

		float delta = Angles.NormalizeAngle( camYaw - bodyYaw );

		// Dans l'arc : rien à faire
		if ( delta >= -max && delta <= max )
			return;

		// Dépassement : report sur le perso, caméra clampée au bord
		if ( delta > max )
		{
			float overshoot = delta - max; // positif
			bodyYaw = NormalizeDeg( bodyYaw + overshoot );
			root.WorldRotation = Rotation.From( new Angles( 0f, bodyYaw, 0f ) );

			_yawDeg = NormalizeDeg( bodyYaw + max );
			return;
		}

		// delta < -max
		{
			float overshoot = delta + max; // négatif
			bodyYaw = NormalizeDeg( bodyYaw + overshoot );
			root.WorldRotation = Rotation.From( new Angles( 0f, bodyYaw, 0f ) );

			_yawDeg = NormalizeDeg( bodyYaw - max );
		}
	}

	// =========================================================
	// ✅ Permissions FP/TP depuis PlayerUiContext
	// =========================================================
	private void ResolveUiContext()
	{
		if ( UiContext != null ) return;

		// caméra peut être sous un GO "Camera", donc on check autour
		UiContext =
			Components.Get<PlayerUiContext>( FindMode.EverythingInSelfAndAncestors )
			?? Components.Get<PlayerUiContext>( FindMode.EverythingInSelfAndDescendants );

		// fallback via Target (si caméra pas dans la hiérarchie directe)
		if ( UiContext == null && Target != null )
		{
			UiContext =
				Target.Components.Get<PlayerUiContext>( FindMode.EverythingInSelfAndDescendants )
				?? Target.Components.Get<PlayerUiContext>( FindMode.EverythingInSelfAndAncestors );
		}
	}

	private bool CanToggleByRules()
	{
		// Sans context => rétro-compat, on bloque rien
		if ( UiContext == null ) return true;

		// toggle uniquement si les 2 vues sont autorisées
		return UiContext.allowFirstPerson && UiContext.allowThirdPerson;
	}

	private void EnforceViewPermissions()
	{
		if ( UiContext == null ) return;

		// Si FP interdit et on est en FP => forcer TP
		if ( Mode == CameraMode.FirstPerson && !UiContext.allowFirstPerson )
			Mode = CameraMode.ThirdPerson;

		// Si TP interdit et on est en TP => forcer FP
		if ( Mode == CameraMode.ThirdPerson && !UiContext.allowThirdPerson )
			Mode = CameraMode.FirstPerson;

		// Si les 2 sont faux, fallback safe
		if ( !UiContext.allowFirstPerson && !UiContext.allowThirdPerson )
			Mode = CameraMode.ThirdPerson;
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
