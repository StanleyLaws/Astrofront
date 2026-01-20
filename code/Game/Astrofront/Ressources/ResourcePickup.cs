using Sandbox;
using System;
using System.Linq;

namespace Astrofront;

public sealed class ResourcePickup : Component
{
    [Property] public ResourceType Type { get; set; } = ResourceType.Stellium;
    [Property] public int Amount { get; set; } = 1;
    [Property] public float UseRadius { get; set; } = 96f;

    // NEW — visuel simple
    [Property, Title("Spin Speed (deg/s)")] public float SpinSpeed { get; set; } = 45f; // NEW
    [Property, Title("Hover Amp")]         public float HoverAmplitude { get; set; } = 4f; // NEW
    [Property, Title("Hover Speed")]       public float HoverSpeed { get; set; } = 2f; // NEW

    private ModelRenderer _renderer;  // NEW
    private Vector3 _spawnPos;        // NEW
	
	
	private void SetVisualScale( float uniformScale )
	{
		var tr = Transform.Local;
		tr.Scale = new Vector3( uniformScale, uniformScale, uniformScale );
		Transform.Local = tr;
	}

	
	
	

    // client local -> serveur : tentative de ramassage
    [Rpc.Host]
	public void TryPickupHost()
	{
		var caller = Rpc.Caller ?? Connection.Local;
		if ( caller is null || Amount <= 0 ) return;

		var inv = FindInventoryFor( caller );
		if ( inv == null ) return;

		// ← capacity-aware avec retour
		int accepted = inv.AddResourceServerReturnAccepted( caller, Type, Amount );

		if ( accepted <= 0 )
		{
			// inventaire plein → on laisse l’objet au sol
			return;
		}

		if ( accepted < Amount )
		{
			// partiel → on réduit la quantité
			Amount -= accepted;
			// (optionnel) ajuste la taille visuelle si tu as ajouté ça
			// SetVisualScale( 0.4f + MathF.Min( 0.6f, Amount * 0.01f ) );
			return;
		}

		// tout pris
		GameObject.Destroy();
	}


    protected override void OnStart()
    {
        // NEW — créer le rendu localement (chaque client)
        _spawnPos = Transform.World.Position;

        _renderer = Components.Get<ModelRenderer>( FindMode.InSelf | FindMode.InChildren );
        if ( _renderer == null )
        {
            _renderer = Components.Create<ModelRenderer>();
            // Cube "dev" basique
            _renderer.Model = Model.Load( "models/dev/box.vmdl" );
        }

        // Teinte par type
        var tint = Type switch
        {
            ResourceType.Stellium => Color.Cyan,     // bleu
            ResourceType.Plasma   => Color.Magenta,  // violet/rose
            ResourceType.Alloy    => Color.Yellow,   // doré
            _ => Color.White
        };
        _renderer.Tint = tint.WithAlpha( 0.95f );

        // Taille indicative (optionnel) selon Amount
        SetVisualScale( 0.4f + MathF.Min( 0.6f, Amount * 0.01f ) );

        // (Optionnel) petit collider de confort
        // var col = Components.Get<SphereCollider>(FindMode.InSelf);
        // if (col == null) { col = Components.Create<SphereCollider>(); col.Radius = 8f; col.IsTrigger = true; }
    }

    protected override void OnUpdate()
    {
        // NEW — idle (spin + hover)
        var tr = Transform.World;
        tr.Rotation *= Rotation.FromAxis( Vector3.Up, SpinSpeed * Time.Delta );
        tr.Position  = _spawnPos + Vector3.Up * (float)Math.Sin( Time.Now * HoverSpeed ) * HoverAmplitude;
        Transform.World = tr;

    }

    private InventorySystem FindInventoryFor( Connection conn )
    {
        var ps = Scene?.GetAllComponents<PlayerState>()
                       ?.FirstOrDefault(p => p != null && p.Network != null && p.Network.Owner == conn);
        if ( ps == null ) return null;
        return ps.GameObject?.Components.Get<InventorySystem>( FindMode.InSelf | FindMode.InChildren );
    }
}
