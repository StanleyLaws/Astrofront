using Sandbox;

namespace Astrofront;

/// À attacher au Prefab "Spectator"
/// - Invisible (désactive les ModelRenderer de l'objet et de ses enfants)
/// - Pas de collisions (désactive Colliders / Rigidbodies de l'objet et de ses enfants)
/// - Noclip free-fly (WASD + Espace↑ / Ctrl↓, Shift = boost)
/// - La caméra suit ce pawn côté propriétaire
public sealed class SpectatorController : Component
{
    [Property] public float BaseSpeed { get; set; } = 300f;
	[Property] public float BoostMultiplier { get; set; } = 4f;
	[Property] public float LookSensitivity { get; set; } = 0.2f;
    [Property] public bool  HideRenderers { get; set; } = true;
    [Property] public bool  DisableCollisions { get; set; } = true;
    [Property] public bool  ControlCamera { get; set; } = true;

    private float _yaw;
    private float _pitch;

    protected override void OnStart()
    {
        // Orientation de départ (depuis la world transform)
        var start = Transform.World;
        _yaw   = start.Rotation.Yaw();
        _pitch = start.Rotation.Pitch();

        if ( HideRenderers )
        {
            // Désactive les ModelRenderer sur ce GO et tous ses enfants (pas d'API includeChildren, on parcourt récursivement)
            ForEachInHierarchy( GameObject, go =>
            {
                foreach ( var r in go.Components.GetAll<ModelRenderer>() )
                    r.Enabled = false;
            });
        }

        if ( DisableCollisions )
        {
            ForEachInHierarchy( GameObject, go =>
            {
                foreach ( var c in go.Components.GetAll<Collider>() )
                    c.Enabled = false;

                foreach ( var rb in go.Components.GetAll<Rigidbody>() )
                    rb.Enabled = false;
            });
        }
    }

    protected override void OnUpdate()
    {
        // Contrôle uniquement par le propriétaire réseau
        if ( Network.Owner != Connection.Local )
            return;

        // ----- Look (souris) -----
        var md = Input.MouseDelta;
		_yaw   -= md.x * LookSensitivity; // ← non inversé (gauche = gauche)
		_pitch += md.y * LookSensitivity; // ← haut = haut
		_pitch  = _pitch.Clamp(-89f, 89f);

		var rot = Rotation.From(new Angles(_pitch, _yaw, 0f));

        // Écriture rotation World (Transform.World est un struct => lire, modifier, réassigner)
        var tr = Transform.World;
        tr.Rotation = rot;
        Transform.World = tr;

        // ----- Move (WASD + jump/duck) -----
        var f = (Input.Down( "forward" ) ? 1f : 0f) - (Input.Down( "backward" ) ? 1f : 0f);
        var r = (Input.Down( "right" )   ? 1f : 0f) - (Input.Down( "left" ) ? 1f : 0f);
        var u = (Input.Down( "jump" )    ? 1f : 0f) - (Input.Down( "duck" ) ? 1f : 0f);

        var wish = (rot.Forward * f) + (rot.Right * r) + (Vector3.Up * u);
        if ( wish.Length > 0f ) wish = wish.Normal;

        float speed = BaseSpeed * (Input.Down( "run" ) ? BoostMultiplier : 1f);

        tr = Transform.World;
        tr.Position += wish * speed * Time.Delta;
        Transform.World = tr;

        // ----- Caméra -----
        if ( ControlCamera && Scene?.Camera is not null )
        {
            // Même pattern de struct pour la caméra de scène
            var camTr = Scene.Camera.Transform.World; 
            camTr.Position = tr.Position;
            camTr.Rotation = tr.Rotation;
            Scene.Camera.Transform.World = camTr;
        }
    }

    // Parcours récursif GameObject + enfants
    private static void ForEachInHierarchy( GameObject root, System.Action<GameObject> action )
    {
        if ( root == null || action == null ) return;

        action( root );

        // Parcours des enfants sans dépendre d'un paramètre includeChildren
        foreach ( var child in root.Children )
        {
            ForEachInHierarchy( child, action );
        }
    }
}
