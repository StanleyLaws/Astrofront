using Sandbox;
using System.Linq;

namespace Astrofront;

/// Permet au joueur local de "réclamer" ses ressources en s'approchant du dispenser et en appuyant sur "use".
/// - À poser sur le même GameObject que StationResourceGenerator.
/// - Fonctionne en local éditeur (pas besoin de dédié).
public sealed class DispenserUse : Component
{
	
	public bool IsLocalInRange { get; private set; } = false;
	public Vector3 WorldPos => GameObject.Transform.World.Position;
	
	
	
    [Property, Title("Rayon d’utilisation (unités)")]
    public float UseRadius { get; set; } = 160f;

    [Property, Title("Cooldown entre 2 utilisations (s)")]
    public float UseCooldown { get; set; } = 0.5f;

    private TimeSince _sinceUse = 999f;
    private StationResourceGenerator _gen;

    protected override void OnStart()
	{
		// 1) D’abord : chercher sur le même GameObject (recommandé)
		_gen = Components.Get<StationResourceGenerator>( FindMode.InSelf );

		// 2) Fallback (optionnel) : si pas trouvé, on cherche le plus proche dans la scène
		if ( _gen == null )
		{
			var myPos = Transform.World.Position;
			_gen = Scene.GetAllComponents<StationResourceGenerator>()
						.OrderBy( g => g.Transform.World.Position.Distance( myPos ) )
						.FirstOrDefault();
		}

		if ( _gen == null )
			Log.Warning("[DispenserUse] StationResourceGenerator introuvable (mets ce script sur le même GO que le dispenser).");
	}


    protected override void OnUpdate()
    {
        if ( _gen == null ) return;

        // Position du joueur local : on prend le PlayerState owner si dispo, sinon la caméra
        var localConn = Connection.Local;
        if ( localConn == null ) return;

        var localPlayer = Scene.GetAllComponents<PlayerState>()
                               .FirstOrDefault(p => p.Network?.Owner == localConn && !p.IsProxy);

        Vector3 playerPos;
        if ( localPlayer != null )
            playerPos = localPlayer.GameObject.Transform.World.Position;
        else if ( Scene?.Camera != null )
            playerPos = Scene.Camera.Transform.World.Position;
        else
            return;

        var dispPos = GameObject.Transform.World.Position;
		var distSqr = playerPos.DistanceSquared( dispPos );
		var rSqr = UseRadius * UseRadius;


        // À portée ?
		IsLocalInRange = distSqr <= rSqr;

		// Appui "use" + anti-spam
		if ( IsLocalInRange && Input.Pressed( "use" ) && _sinceUse > UseCooldown )
		{
			_sinceUse = 0f;
			_gen.ClaimAllHost(); // [Rpc.Host]
		}
			}

#if TOOLS
    // Petit gizmo pour voir le rayon dans l’éditeur
    protected override void OnEditorUpdate()
    {
        var tr = Transform.World;
        Gizmo.Draw.Color = Color.Cyan.WithAlpha(0.2f);
        Gizmo.Draw.WireSphere( tr.Position, UseRadius );
    }
#endif
}
