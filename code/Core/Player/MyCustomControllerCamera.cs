using Sandbox;

namespace Astrofront;

/// Caméra TPS "arc arrière" avec :
/// - inversion des axes souris (bas->monte, droite->droite),
/// - pas de molette (distance fixe),
/// - orbite confinée derrière le joueur,
/// - si on pousse au-delà de l'arc : on ROTATE le joueur en direct pour suivre.
public sealed class MyCustomControllerCamera : Component
{
    [Property] public GameObject Target { get; set; }           // joueur racine
    [Property] public float Distance { get; set; } = 180f;      // distance caméra (fixe)
    [Property] public float HeightOffset { get; set; } = 20f;   // hauteur au-dessus du joueur

    [Property] public float MinPitch { get; set; } = -10f;      // bornes verticales (°)
    [Property] public float MaxPitch { get; set; } = 45f;

    [Property] public float MouseSensitivity { get; set; } = 0.15f;

    /// Ouverture latérale (demi-arc) derrière le joueur, en degrés.
    /// Exemple: mets 45 pour ce que tu décris.
    [Property] public float RearArcHalfAngle { get; set; } = 45f;

    /// Si vrai, quand la souris “tire” au-delà de l’arc arrière,
    /// on pivote le joueur pour suivre le mouvement tout en gardant la caméra dans l’arc.
    [Property] public bool RotateTargetWhenArcExceeded { get; set; } = true;

    [Property] public float PosLerp { get; set; } = 12f;        // smoothing position
    [Property] public float RotLerp { get; set; } = 16f;        // smoothing rotation

    private float _yawDeg;    // en degrés (monde)
    private float _pitchDeg;  // en degrés
    private Vector3 _goalPos;
    private Rotation _goalRot;
	
	private CameraComponent _cam;


    protected override void OnStart()
    {
		
		_cam = Components.Get<CameraComponent>( FindMode.EverythingInSelfAndDescendants );
		if ( _cam == null ) return;

		if ( Target is null ) return;


        // On démarre au MILIEU de l’arc arrière du joueur :
        float frontYaw = GetYawDeg( Target.WorldRotation.Forward );
        _yawDeg   = frontYaw; // (la position sera "derrière" via -rot.Forward * Distance)
        _pitchDeg = ((MinPitch + MaxPitch) * 0.5f).Clamp( MinPitch, MaxPitch );
    }

    protected override void OnUpdate()
    {
		
		if ( IsProxy ) return;

		if ( _cam == null || !_cam.Enabled ) return;


		
		
		
        if ( Target is null ) return;

        // 1) Lire la souris - INVERSION demandée :
        //    - souris vers le bas => cam MONTE  (pitch++)
        //    - souris vers la droite => cam va à DROITE du perso (yaw--)
        var md = Input.MouseDelta;
        _yawDeg   -= md.x * MouseSensitivity;   // inversé
        _pitchDeg += md.y * MouseSensitivity;   // inversé
        _pitchDeg  = _pitchDeg.Clamp( MinPitch, MaxPitch );

        // 2) Confinement à l’arc arrière + rotation du joueur si nécessaire
        float frontYaw = GetYawDeg( Target.WorldRotation.Forward );

        // Angle relatif demandé par la souris par rapport au "front" du joueur
        float relDesired = ShortestAngleDeg( _yawDeg, frontYaw ); // [-180,180]

        // Clamp dans l'arc
        float clamped = relDesired.Clamp( -RearArcHalfAngle, +RearArcHalfAngle );

        if ( RotateTargetWhenArcExceeded && !relDesired.AlmostEqual( clamped, 0.0001f ) )
        {
            // On a "poussé" au-delà de l'arc : l'excès devient une rotation du joueur.
            float overflow = relDesired - clamped; // signe = sens de la poussée
            float newFront = NormalizeDeg( frontYaw + overflow );

            // On applique le nouveau yaw au joueur (Z-up, pas de pitch/roll).
            var targetAngles = Target.WorldRotation.Angles();
            targetAngles = new Angles( 0f, newFront, 0f );
            Target.WorldRotation = Rotation.From( targetAngles );

            // La caméra reste dans l'arc autour du NOUVEAU front :
            frontYaw = newFront;
        }

        // Le yaw caméra final reste confiné à l’arc
        _yawDeg = NormalizeDeg( frontYaw + clamped );

        // 3) Construire la rotation de la caméra
        var rot = Rotation.From( new Angles( _pitchDeg, _yawDeg, 0f ) );
        _goalRot = rot;

        // 4) Position : derrière la cible, à Distance, regardant vers la cible
        var focus = Target.WorldPosition + Vector3.Up * HeightOffset;
        var desiredPos = focus - rot.Forward * Distance;
        _goalPos = desiredPos;

        // 5) Lerp doux
        WorldRotation = Rotation.Lerp( WorldRotation, _goalRot, RotLerp * Time.Delta );
        WorldPosition = Vector3.Lerp( WorldPosition, _goalPos, PosLerp * Time.Delta );
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
        var f2 = forward.WithZ(0).Normal;
        if ( f2.IsNearlyZero() ) return 0f;
        return System.MathF.Atan2( f2.y, f2.x ) * 180f / System.MathF.PI;
    }
}
