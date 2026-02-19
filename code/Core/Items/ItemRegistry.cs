using Sandbox;
using System;
using System.Collections.Generic;

namespace Astrofront;

public static class ItemRegistry
{
	private static Dictionary<string, ItemDefinition> _byId;
	private static bool _built;

	public static void Rebuild()
	{
		_byId = new Dictionary<string, ItemDefinition>( StringComparer.OrdinalIgnoreCase );

		var all = ResourceLibrary.GetAll<ItemDefinition>();
		if ( all != null )
		{
			foreach ( var def in all )
			{
				if ( def == null ) continue;
				if ( string.IsNullOrEmpty( def.Id ) ) continue;

				if ( _byId.ContainsKey( def.Id ) )
				{
					Log.Warning( $"[ItemRegistry] Duplicate ItemId '{def.Id}'. Keeping first." );
					continue;
				}

				_byId.Add( def.Id, def );
			}
		}

		_built = true;
		Log.Info( $"[ItemRegistry] Rebuilt. Items={_byId.Count}" );
	}

	private static void Ensure()
	{
		if ( _built && _byId != null ) return;
		Rebuild();
	}

	public static ItemDefinition Get( string id )
	{
		Ensure();
		if ( string.IsNullOrEmpty( id ) ) return null;
		return _byId.TryGetValue( id, out var def ) ? def : null;
	}

	public static GameObject GetItemPrefab( string id )
	{
		return Get( id )?.ItemPrefab; 
	}

	public static string GetName( string id ) => Get( id )?.DisplayName ?? (id ?? "");
	public static string GetUiClass( string id ) => Get( id )?.UiClass ?? "type-generic";
	public static int GetMaxStack( string id ) => Math.Max( 1, Get( id )?.MaxStack ?? 1 );
	public static int GetSpaceCost( string id ) => Math.Max( 1, Get( id )?.SpaceCost ?? 1 );

	public static string GetIcon( string id )
	{
		var path = Get( id )?.Icon;
		return string.IsNullOrEmpty( path ) ? null : path;
	}

	public static string GetIconOrFallback( string id )
	{
		var icon = GetIcon( id );
		return !string.IsNullOrEmpty( icon ) ? icon : "/ui/icons/item.png";
	}
}
