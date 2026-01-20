using Sandbox;
using System;

namespace Astrofront;

/// Pilote citizen.vanmgrph à partir de MyCustomController + Camera.
/// Ne gère QUE l'animation (pas la physique).
public sealed class CitizenAnimDriver : Component
{
    [Property, Group("Refs")] public MyCustomController Controller { get; set; }
    [Property, Group("Refs")] public SkinnedModelRenderer Body { get; set; }
    [Property, Group("Refs")] public CameraComponent Camera { get; set; }

    [Property, Group("Graph")] public AnimationGraph AnimationGraph { get; set; }
    [Property, Group("Graph")] public string GraphPath { get; set; } = "models/citizen/citizen.vanmgrph";

    [Property, Group("Tuning")] public float WalkRunBlendSpeed { get; set; } = 220f;

    [Property, Group("Aim")] public float AimStrengthEyes { get; set; } = 1f;
    [Property, Group("Aim")] public float AimStrengthHead { get; set; } = 1f;
    [Property, Group("Aim")] public float AimStrengthBody { get; set; } = 0.2f;
    [Property, Group("Aim")] public bool AimInLocalSpace { get; set; } = true;

    [Property, Group("Debug")] public bool Logs { get; set; } = false;

    private Vector3 _lastPos;

    // --- Synchronisation réseau de la direction d’aim ---
    [Sync] private Vector3 _syncedAimDir { get; set; } = Vector3.Forward;
    private RealTimeSince _timeSinceAimSent;

    // (on garde ces champs si tu veux re-tester plus tard)
    private float _lastDuck;
    private RealTimeSince _sinceDuckChange;

    protected override void OnStart()
    {
        if (Body is null) Body = GameObject.GetComponentInChildren<SkinnedModelRenderer>();
        if (Camera is null && Controller != null) Camera = Controller.Camera;

        if (Body == null)
        {
            Log.Warning("[Anim] Pas de SkinnedModelRenderer (Body) trouvé.");
            return;
        }

        if (Body.AnimationGraph == null)
        {
            if (AnimationGraph == null && !string.IsNullOrEmpty(GraphPath))
                AnimationGraph = ResourceLibrary.Get<AnimationGraph>(GraphPath);

            if (AnimationGraph != null)
            {
                Body.UseAnimGraph = true;
                Body.AnimationGraph = AnimationGraph;
            }
        }

        // Defaults neutres
        Body.Set("move_style", 0);
        Body.Set("special_movement_states", 0);
        Body.Set("holdtype", 0);
        Body.Set("sit", 0f);
        Body.Set("duck", 0f);
        Body.Set("b_noclip", false);
        Body.Set("b_swim", false);
        Body.Set("b_firstperson", false);
        Body.Set("b_reload", false);
        Body.Set("b_reloading", false);
        Body.Set("b_reloading_insert", false);
        Body.Set("b_attack", false);
        Body.Set("b_weapon_lower", false);
        Body.Set("voice", 0f);

        var graphName = Body.AnimationGraph != null ? Body.AnimationGraph.ResourcePath : "<NULL>";
        Log.Info($"[Anim] UseAnimGraph={Body.UseAnimGraph}  Graph={graphName}");

        _lastPos = Controller != null ? Controller.WorldPosition : WorldPosition;
        _lastDuck = Controller != null ? Controller.DuckAmount : 0f;
        _sinceDuckChange = 999f;
    }

    protected override void OnUpdate()
{
    if (Body == null || !Body.UseAnimGraph || Body.AnimationGraph == null) return;

    bool isLocalOwner =
        (Network != null && Network.Owner == Connection.Local)
        || (!IsProxy && Connection.Local != null);

    // --- Lire état duck + détecter la frame de sortie ---
    float duckVal = (Controller != null) ? Controller.DuckAmount : 0f;
    bool justExitedDuck = false;
    if (MathF.Abs(duckVal - _lastDuck) > 0.02f)
    {
        _sinceDuckChange = 0f;
        // sortie du duck quand on passe sous 0.5
        if (_lastDuck >= 0.5f && duckVal < 0.5f)
            justExitedDuck = true;

        _lastDuck = duckVal;
    }

    // Multiplieur de vitesse cohérent avec la physique
    float duckSpeedMul = 1f;
    if (Controller != null)
        duckSpeedMul = MathX.Lerp(1f, Controller.DuckSpeedMultiplier, duckVal);

    // --- Intentions (wish_*) ---
    float wish_x = 0f, wish_y = 0f, wish_speed = 0f, wish_direction = 0f;
    bool hasMoveInput = false;

    if (isLocalOwner && !LootController.IsUiLockedLocal)
    {
        var mv = Input.AnalogMove; // x=right, y=forward
        var wish2 = new Vector2(mv.x, mv.y);
        float mag = wish2.Length.Clamp(0f, 1f);

        // citizen.vanmgrph : x=forward, y=right
        wish_x       =  ( mv.y * WalkRunBlendSpeed ) * duckSpeedMul;
        wish_y       = -( mv.x * WalkRunBlendSpeed ) * duckSpeedMul;
        wish_speed   =    mag * WalkRunBlendSpeed    * duckSpeedMul;
        hasMoveInput =  mag > 0.01f;

        if (mag > 0.001f)
            wish_direction = MathF.Atan2(wish_y, wish_x) * 180f / MathF.PI;
    }

    // --- Mouvement réel (move_*) ---
    var pos = Controller != null ? Controller.WorldPosition : WorldPosition;
    var velFromPos = (pos - _lastPos) / Time.Delta; _lastPos = pos;

    // Utilise la vélocité physique si dispo (instantanée)
    var vel = Controller != null ? Controller.Velocity : velFromPos;

    var fwd   = GameObject.WorldRotation.Forward.WithZ(0f).Normal;
    var right = Vector3.Up.Cross(fwd).Normal;
    var velFlat = vel.WithZ(0f);

    float fwdSpd  = velFlat.Dot(fwd);
    float sideSpd = -velFlat.Dot(right);
    float move_x  = fwdSpd;
    float move_y  = sideSpd;
    float move_spd = MathF.Sqrt(fwdSpd * fwdSpd + sideSpd * sideSpd);
    float move_direction = (move_spd > 0.01f)
        ? MathF.Atan2(move_y, move_x) * 180f / MathF.PI
        : 0f;

    // UI lock → idle propre
    if (isLocalOwner && LootController.IsUiLockedLocal)
    {
        wish_x = wish_y = wish_speed = 0f;
        hasMoveInput = false;
        move_x = move_y = move_spd = 0f;
    }

    bool grounded = ProbeGround(pos, Controller != null ? Controller.Radius : 5f);

    // --- Duck vers graph ---
    Body.Set("duck", duckVal);
    Body.Set("b_ducking", duckVal > 0.5f);

    // --- AIM ---
    Vector3 aimDir;
    if (isLocalOwner && Camera != null)
    {
        var camFwd = Camera.WorldRotation.Forward.Normal;
        aimDir = AimInLocalSpace
            ? (GameObject.WorldRotation.Inverse * camFwd).Normal
            : camFwd;

        if (_timeSinceAimSent > 0.1f && _syncedAimDir.Distance(aimDir) > 0.01f)
        {
            _timeSinceAimSent = 0;
            RpcSyncAim(aimDir);
        }
    }
    else
    {
        aimDir = _syncedAimDir;
    }

    Body.Set("aim_body", aimDir);
    Body.Set("aim_head", aimDir);
    Body.Set("aim_eyes", aimDir);
    Body.Set("aim_body_weight", AimStrengthBody);
    Body.Set("aim_head_weight", AimStrengthHead);
    Body.Set("aim_eyes_weight", AimStrengthEyes);

    // --- Push intentions et mouvements ---
    Body.Set("wish_x", wish_x);
    Body.Set("wish_y", wish_y);
    Body.Set("wish_speed", wish_speed);
    Body.Set("wish_groundspeed", wish_speed);
    Body.Set("wish_direction", wish_direction);
    Body.Set("has_move_input", hasMoveInput);

    Body.Set("move_x", move_x);
    Body.Set("move_y", move_y);
    Body.Set("move_speed", move_spd);
    Body.Set("move_groundspeed", move_spd);
    Body.Set("move_direction", move_direction);

    // ➜ Anti-flash d'idle : la frame où on sort du duck + input présent -> moving true
    Body.Set("moving", (move_spd > 1.0f) || (justExitedDuck && hasMoveInput));

    Body.Set("b_grounded", grounded);
    Body.Set("b_grouned", grounded); // compat
}


    // RPC — broadcast direction d’aim
    [Rpc.Broadcast]
    private void RpcSyncAim(Vector3 dir)
    {
        _syncedAimDir = dir.Normal;
    }

    bool ProbeGround(Vector3 pos, float radius)
    {
        var tr = Scene.Trace.Ray(pos + Vector3.Up * 1f, pos + Vector3.Down * 6f)
            .Radius(radius * 0.95f)
            .IgnoreGameObject(GameObject) 
            .Run();
        return tr.Hit; 
    }
}
