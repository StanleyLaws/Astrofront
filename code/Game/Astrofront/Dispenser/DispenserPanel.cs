using System;
using Sandbox;

public sealed class DispenserPanel : Component
{
    [Property, Title("Forward Offset (towards camera)")]
    public float ForwardOffset { get; set; } = 90f;

    [Property, Title("Vertical Offset")]
    public float VerticalOffset { get; set; } = 8f;

    [Property] public bool  Hover { get; set; } = false;
    [Property] public float HoverAmplitude { get; set; } = 4f;
    [Property] public float HoverSpeed { get; set; } = 1.5f;

    protected override void OnUpdate()
    {
        if ( Scene?.Camera is null || GameObject.Parent is null )
            return;

        var basePos = GameObject.Parent.Transform.World.Position;
        var camPos  = Scene.Camera.Transform.World.Position;
        var dirToCam = (camPos - basePos).Normal;

        var pos = basePos + dirToCam * ForwardOffset + Vector3.Up * VerticalOffset;

        if ( Hover )
        {
            pos += Vector3.Up * (float)Math.Sin( Time.Now * HoverSpeed ) * HoverAmplitude;
        }

        var tr = Transform.World;
        tr.Position = pos;
        Transform.World = tr;   // NE PAS toucher à tr.Rotation : LookAtCamera s’en occupe
    }
}
