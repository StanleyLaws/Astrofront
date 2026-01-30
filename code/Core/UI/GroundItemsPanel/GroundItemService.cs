using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astrofront;

public static class GroundItemsService
{
	public static float ScanRadius { get; set; } = 160f;

	public static void OpenLootForLocal()
	{
		// Si déjà ouvert, juste refresh
		if ( GroundItemsPanel.Instance?.IsOpen == true )
		{
			RefreshLootForLocal();
			return;
		}

		RefreshLootForLocal(); // Show(...) met IsOpen=true
	}

	public static void RefreshLootForLocal()
	{
		var scene = Game.ActiveScene;
		if ( scene == null ) return;

		var ps = FindLocalPlayerState( scene );
		if ( ps == null ) return;

		var pos = ps.Transform.World.Position;
		float r2 = ScanRadius * ScanRadius;

		// Agrégation par ItemId
		var dict = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );

		foreach ( var p in scene.GetAllComponents<GroundItemPickup>() )
		{
			if ( p == null ) continue;
			if ( p.Amount <= 0 ) continue;

			var id = p.ItemId;
			if ( string.IsNullOrEmpty( id ) ) continue;

			if ( (p.Transform.World.Position - pos).LengthSquared > r2 )
				continue;

			dict.TryGetValue( id, out var cur );
			dict[id] = cur + p.Amount;
		}

		// ✅ IMPORTANT : publier le snapshot pour recaler la main fantôme si elle vient du loot
		UiDragReconciler.UpdateLootAvailability( dict );

		// Convert -> DTO (ItemId + Amount)
		GroundItemDto[] items;

		if ( dict.Count == 0 ) 
		{
			items = Array.Empty<GroundItemDto>();
		}
		else
		{
			items = dict
				.Select( kv => new GroundItemDto( kv.Key, kv.Value ) )
				.OrderByDescending( x => x.Amount )
				.ToArray();
		}

		GroundItemsPanel.Show( items );
	}

	private static PlayerState FindLocalPlayerState( Scene scene )
	{
		var ps = scene.GetAllComponents<PlayerState>()
			.FirstOrDefault( p => p != null && !p.IsProxy && p.GameObject != null && p.GameObject.Tags.Has( "localplayer" ) );

		if ( ps != null ) return ps;

		var local = Connection.Local;
		if ( local == null ) return null;

		return scene.GetAllComponents<PlayerState>()
			.FirstOrDefault( p => p != null && !p.IsProxy && p.Network?.Owner == local );
	}
}
