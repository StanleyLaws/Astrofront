using Sandbox;
using System.Linq;

namespace Astrofront;

/// <summary>
/// Item au sol (réseau) : source de vérité monde.
/// </summary>
public sealed class GroundItemPickup : Component
{
	[Sync] public string ItemId { get; set; }
	[Sync] public int Amount { get; set; } = 1;

	[Property] public float UseRadius { get; set; } = 96f;

	private ModelRenderer _renderer;

	protected override void OnStart()
	{
		// Visuel local pour chaque client
		_renderer = Components.Get<ModelRenderer>( FindMode.InSelf | FindMode.InChildren );
		if ( _renderer == null )
			_renderer = Components.Create<ModelRenderer>();

		RefreshVisual();
	}

	protected override void OnUpdate()
	{
		// Si l'id change (synchro), on refresh (simple, safe)
		// (Tu peux optimiser plus tard)
		if ( _renderer != null && _renderer.Model == null )
			RefreshVisual();
	}

	private void RefreshVisual()
	{
		if ( _renderer == null ) return;

	}

	/// <summary>
	/// Tentative de pickup (client -> host).
	/// </summary>
	[Rpc.Host]
	public void TryPickupHost()
	{
		var caller = Rpc.Caller ?? Connection.Local;
		if ( caller == null ) return;
		if ( Amount <= 0 ) return;

		var ps = Scene?.GetAllComponents<PlayerState>()
			?.FirstOrDefault( p => p != null && p.Network != null && p.Network.Owner == caller );

		if ( ps == null ) return;

		// distance check côté serveur (anti-cheat / autorité)
		var pos = ps.Transform.World.Position;
		if ( (Transform.World.Position - pos).LengthSquared > UseRadius * UseRadius )
			return;

		var inv = ps.GameObject.Components.Get<InventoryComponent>( FindMode.InSelf | FindMode.InChildren );
		if ( inv == null ) return;

		// Ajout autoritaire
		var added = inv.AddHost( ItemId, Amount );
		if ( added <= 0 ) return;

		if ( added < Amount )
		{
			Amount -= added; // reste au sol
			return;
		}

		// Tout pris
		GameObject.Destroy();
	}
}
