using Sandbox;

namespace Astrofront;

public sealed class MyCustomController : Component
{
    // --- Camera / Orientation ---
    [Property, Group("Camera")] public CameraComponent Camera { get; set; }
    [Property, Group("Camera")] public bool  AlignToCameraYaw { get; set; } = true;
    [Property, Group("Camera")] public float AlignRotateSpeed { get; set; } = 10f;

    // --- Collision / Mouvement ---
    [Property, Group("Collision")] public float Radius     { get; set; } = 5f;
    [Property, Group("Movement")]  public float MoveSpeed  { get; set; } = 220f;
    [Property, Group("Movement")]  public float Accel      { get; set; } = 10f;
    [Property, Group("Movement")]  public float Gravity    { get; set; } = 900f;
    [Property, Group("Movement")]  public int   SlideIters { get; set; } = 3;

    // Stick to ground (post-move)
    [Property, Group("Movement")] public float GroundSnapDist   { get; set; } = 6f;
    [Property, Group("Movement")] public float GroundMinNormalZ { get; set; } = 0.5f; // ~60°
    [Property, Group("Movement")] public float MaxSnapSpeed     { get; set; } = 140f; // (réservé)
    [Property, Group("Movement")] public float SnapRadiusScale  { get; set; } = 0.75f;
    [Property, Group("Movement")] public float SupportProbeAhead { get; set; } = 0.6f;

    // --- Jump ---
    [Property, Group("Jump")] public float JumpSpeed      { get; set; } = 360f;
    [Property, Group("Jump")] public float CoyoteTime     { get; set; } = 0.12f;
    [Property, Group("Jump")] public float JumpBuffer     { get; set; } = 0.12f;
    [Property, Group("Jump")] public float JumpNoSnapTime { get; set; } = 0.20f;

    // --- Duck (maintenu) ---
    [Property, Group("Duck")] public float DuckInSpeed         { get; set; } = 12f;
    [Property, Group("Duck")] public float DuckOutSpeed        { get; set; } = 40f;
    [Property, Group("Duck")] public float DuckRadiusScale     { get; set; } = 0.80f;
    [Property, Group("Duck")] public float DuckSpeedMultiplier { get; set; } = 0.55f;
    [Property, Group("Duck")] public float UncrouchHeadroom    { get; set; } = 18f;

    // --- Debug ---
    [Property, Group("Debug")] public bool  DebugLogs  { get; set; } = true;
    [Property, Group("Debug")] public float DebugEvery { get; set; } = 0.30f;

    private Vector3 _velocity;
    private bool _isGrounded;

    // Jump helpers
    private float _coyoteUntil;
    private float _jumpQueuedUntil = float.NegativeInfinity;

    // Input capté en Update
    private bool _jumpPressedFrame;

    // Duck interne (0..1)
    private float _duck;       // 0 debout, 1 accroupi
    private float _duckPrev;   // <- pour détecter la sortie du duck cette frame
    private bool  _justExitedDuck; // <- flag 1-frame lors de la remontée

    // Debug throttle
    private float _nextDebugAt;

    // Exposés
    public bool IsGrounded => _isGrounded;
    public Vector3 Velocity => _velocity;
    public TimeSince TimeSinceJump { get; private set; } = 999f;

    public float DuckAmount => _duck;
    public bool  IsDucking  => _duck > 0.5f;

    private float ColRadius => Radius * (1f - _duck * (1f - DuckRadiusScale));

    protected override void OnUpdate()
    {
        // Ancien: InputGate.CanGameplayInput
        bool canInput = !UiModalController.IsUiLockedLocal;

        // Pas de jump-buffer pendant le lock
        if ( canInput && Input.Pressed( InputActions.Jump ) )
            _jumpPressedFrame = true;
        else if ( !canInput )
            _jumpPressedFrame = false;

        if ( DebugLogs && Time.Now >= _nextDebugAt )
        {
            _nextDebugAt = Time.Now + DebugEvery;
            Log.Info($"[CTRL][Update][{GameObject?.Name}] IsProxy={IsProxy} UILock={!canInput} Pressed={_jumpPressedFrame}");
        }
    }

    protected override void OnFixedUpdate()
    {
        if ( IsProxy ) return;

        // Ancien: InputGate.CanGameplayInput
        bool canInput = !UiModalController.IsUiLockedLocal;

        // ---------- Entrées ----------
        bool f = canInput && Input.Down( InputActions.Forward );
        bool b = canInput && Input.Down( InputActions.Backward );
        bool l = canInput && Input.Down( InputActions.Left );
        bool r = canInput && Input.Down( InputActions.Right );
        bool wantDuckHold = canInput && Input.Down( InputActions.Duck );

        float axForward = (f ? 1f : 0f) - (b ? 1f : 0f);
        float axRight   = (r ? 1f : 0f) - (l ? 1f : 0f);

        Rotation basisRot = WorldRotation;
        if ( Camera is not null )
        {
            var camFwdFlat = Camera.WorldRotation.Forward.WithZ(0);
            if ( !camFwdFlat.IsNearlyZero() )
                basisRot = Rotation.LookAt( camFwdFlat.Normal, Vector3.Up );
        }

        var fwd   = basisRot.Forward;
        var right = fwd.Cross( Vector3.Up ).Normal;

        var wishDir = ( fwd * axForward + right * axRight );
        if ( wishDir.LengthSquared > 1e-6f ) wishDir = wishDir.Normal;

        // Vitesse base → ralentie si duck (blend propre 0..1)
        float speedMul = MathX.Lerp( 1f, DuckSpeedMultiplier, _duck );
        var wishVel = wishDir * (MoveSpeed * speedMul);

        // Pas de rotation auto pendant l’UI lock
        if ( canInput && AlignToCameraYaw && wishDir.LengthSquared > 1e-6f )
        {
            var targetYaw = Rotation.LookAt( fwd, Vector3.Up );
            WorldRotation = Rotation.Lerp( WorldRotation, targetYaw, AlignRotateSpeed * Time.Delta );
        }

        // ---------- Duck (blend + headroom) ----------
        _justExitedDuck = false; // reset 1-frame

        float duckTarget = wantDuckHold ? 1f : 0f;

        if ( !canInput )
        {
            duckTarget = _duck; // on gèle pendant le lock
        }
        else
        {
            // Si on veut se relever, vérifier plafond (headroom)
            if ( duckTarget < 0.5f && _duck > 0.01f )
            {
                bool blocked = !HasHeadroomToUncrouch();
                if ( DebugLogs ) Log.Info($"[CTRL][DuckCheck][{GameObject?.Name}] wantUncrouch -> headroom={(blocked ? "BLOCKED" : "OK")}");
                if ( blocked ) duckTarget = 1f;
            }
        }

        float duckLerpSpeed = (duckTarget > _duck) ? DuckInSpeed : DuckOutSpeed;
        float duckBefore = _duck;
        _duck = _duck.LerpTo( duckTarget, duckLerpSpeed * Time.Delta );

        // Détection sortie du duck (remontée)
        if ( duckBefore >= 0.5f && _duck < 0.5f )
            _justExitedDuck = true;

        _duckPrev = _duck;

        // ---------- Gravité & accél latérale ----------
        _velocity.z -= Gravity * Time.Delta;

        bool hasWish = wishDir.LengthSquared > 1e-6f;

        // KICKSTART : si on vient de sortir du duck, qu'on est au sol et qu'il y a un input,
        // on applique directement la vitesse horizontale cible cette frame.
        if ( _justExitedDuck && hasWish && _isGrounded )
        {
            _velocity = _velocity.WithX( wishVel.x ).WithY( wishVel.y );
        }
        else
        {
            _velocity = _velocity
                .WithX( _velocity.x.LerpTo( wishVel.x, Accel * Time.Delta ) )
                .WithY( _velocity.y.LerpTo( wishVel.y, Accel * Time.Delta ) );
        }

        // ---------- Jump : buffer + coyote ----------
        if ( !canInput )
        {
            _jumpQueuedUntil = float.NegativeInfinity;
        }
        else
        {
            if ( _jumpPressedFrame )
            {
                _jumpQueuedUntil = Time.Now + JumpBuffer;
                _jumpPressedFrame = false;
                if ( DebugLogs ) Log.Info($"[CTRL][Fixed][{GameObject?.Name}] Jump pressed → buffer until {_jumpQueuedUntil:F2} (now {Time.Now:F2})");
            }

            if ( _isGrounded )
                _coyoteUntil = Time.Now + CoyoteTime;

            TryConsumeJump();
        }

        // ---------- Déplacement (resolver 3 passes v2) ----------
        var delta = _velocity * Time.Delta;
        WorldPosition = Move3PassResolver(WorldPosition, delta, ColRadius);

        // ---------- Post-move : snap au sol ----------
        bool allowSnap = TimeSinceJump > JumpNoSnapTime && _velocity.z <= 0f;

        if ( allowSnap )
            _isGrounded = TrySnapToGround(ColRadius);
        else
            _isGrounded = false;

        if ( _isGrounded )
            _velocity = _velocity.WithZ( 0f );

        if ( DebugLogs && Time.Now >= _nextDebugAt )
        {
            _nextDebugAt = Time.Now + DebugEvery;
            Log.Info($"[CTRL][Fixed][{GameObject?.Name}] grounded={_isGrounded} velZ={_velocity.z:F1} now={Time.Now:F2} sinceJump={TimeSinceJump:F2} duck={_duck:F2} allowSnap={allowSnap} UILock={!canInput}");
        }
    }

    private void TryConsumeJump()
    {
        bool buffered = Time.Now <= _jumpQueuedUntil;
        bool canJump  = _isGrounded || Time.Now <= _coyoteUntil;

        if ( DebugLogs )
            Log.Info($"[CTRL][JumpCheck][{GameObject?.Name}] buffered={buffered} canJump={canJump} grounded={_isGrounded} now={Time.Now:F2} until(buf)={_jumpQueuedUntil:F2} until(coy)={_coyoteUntil:F2}");

        if ( buffered && canJump )
        {
            _jumpQueuedUntil = float.NegativeInfinity;
            _coyoteUntil = 0f;
            _isGrounded = false;

            _velocity = _velocity.WithZ( JumpSpeed );
            TimeSinceJump = 0f;

            Log.Info($"[CTRL][JUMP][{GameObject?.Name}] IMPULSE +{JumpSpeed}z  velZ={_velocity.z:F1}");
        }
    }

    // ====== 3 passes v2 : Up -> Horizontal -> Down ======
    private Vector3 Move3PassResolver( Vector3 start, Vector3 delta, float radius )
    {
        var pos = start;
        float dz = delta.z;
        Vector3 horiz = delta.WithZ(0f);

        float eps = System.MathF.Max( radius * 0.01f, 0.05f );
        float groundZ = 0.6f;   // seuil "c'est le sol"

        // ---- 1) Vertical UP ----
        if ( dz > 0f )
        {
            var from = pos + Vector3.Up * System.MathF.Max( eps, radius * 0.05f );
            var to   = from + Vector3.Up * dz;

            var trUp = Scene.Trace
                .Ray( from, to )
                .Radius( radius )
                .IgnoreGameObject( GameObject )
                .Run();

            if ( !trUp.Hit )
            {
                pos = to;
            }
            else
            {
                pos = trUp.EndPosition + trUp.Normal * eps;
                if ( trUp.Normal.z < -groundZ && _velocity.z > 0f )
                    _velocity = _velocity.WithZ( 0f );
            }
        }

        // ---- 2) Horizontal (slide murs/pentes ; ignore sol/plafond) ----
        var remaining = horiz;
        for ( int i = 0; i < SlideIters; i++ )
        {
            if ( remaining.LengthSquared.AlmostEqual(0f) ) break;

            var tr = Scene.Trace
                .Ray( pos, pos + remaining )
                .Radius( radius )
                .IgnoreGameObject( GameObject )
                .Run();

            if ( !tr.Hit )
            {
                pos += remaining;
                break;
            }

            if ( tr.Normal.z >= groundZ || tr.Normal.z <= -groundZ )
            {
                if ( tr.Normal.z > 0f ) pos += Vector3.Up * eps;
                pos = tr.EndPosition + tr.Normal * eps;
                continue;
            }

            pos = tr.EndPosition + tr.Normal * eps;
            var n = tr.Normal.Normal;
            remaining = remaining - n * remaining.Dot(n);
        }

        // ---- 3) Vertical DOWN ----
        if ( dz < 0f )
        {
            var trDown = Scene.Trace
                .Ray( pos, pos + Vector3.Down * -dz )
                .Radius( radius )
                .IgnoreGameObject( GameObject )
                .Run();

            if ( !trDown.Hit )
            {
                pos += Vector3.Down * -dz;
            }
            else
            {
                pos = trDown.EndPosition + trDown.Normal * eps;
            }
        }

        return pos;
    }

    private bool TrySnapToGround(float radius)
    {
        var start = WorldPosition;
        var end   = start + Vector3.Down * GroundSnapDist;

        var tr = Scene.Trace
            .Ray( start, end )
            .Radius( radius * 0.98f )
            .IgnoreGameObject( GameObject )
            .Run();

        if ( tr.Hit && tr.Normal.z >= GroundMinNormalZ )
        {
            WorldPosition = tr.EndPosition;
            return true;
        }

        return false;
    }

    private bool HasHeadroomToUncrouch()
    {
        float standR   = Radius * 0.98f;
        float currentR = ColRadius;

        float rise = System.MathF.Max( UncrouchHeadroom, standR * 0.8f );
        float eps = System.MathF.Max( standR * 0.02f, 0.5f );

        var from = WorldPosition + Vector3.Up * eps;
        var to   = from + Vector3.Up * rise;

        var upTrace = Scene.Trace
            .Ray( from, to )
            .Radius( currentR )
            .IgnoreGameObject( GameObject )
            .Run();

        if ( upTrace.Hit ) return false;

        var top = to;
        var standCheck = Scene.Trace
            .Ray( top, top + Vector3.Up * eps )
            .Radius( standR )
            .IgnoreGameObject( GameObject )
            .Run();

        return !standCheck.Hit;
    }
}
