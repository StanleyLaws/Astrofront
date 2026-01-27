using Sandbox;
using System;
using System.Collections.Generic;

namespace Astrofront;

public static class ItemRegistry
{
	// Id -> Definition
	private static Dictionary<string, ItemDefinition> _byId;

	// WorldModel path -> Model cache
	private static Dictionary<string, Model> _modelCache;

	private static bool _built;

	/// <summary>
	/// Rebuild depuis les assets ItemDefinition présents dans ResourceLibrary.
	/// Appelle ça si tu ajoutes/modifies des assets et que tu veux forcer le refresh.
	/// </summary>
	public static void Rebuild()
	{
		_modelCache ??= new Dictionary<string, Model>( StringComparer.OrdinalIgnoreCase );
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
					Log.Warning( $"[ItemRegistry] Duplicate ItemId '{def.Id}' found in ItemDefinition assets. Keeping first." );
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

	public static bool Exists( string id ) => Get( id ) != null;

	public static string GetName( string id ) => Get( id )?.DisplayName ?? (id ?? "");
	public static string GetUiClass( string id ) => Get( id )?.UiClass ?? "type-generic";
	public static int GetMaxStack( string id ) => Math.Max( 1, Get( id )?.MaxStack ?? 1 );
	public static int GetSpaceCost( string id ) => Math.Max( 1, Get( id )?.SpaceCost ?? 1 );

	public static Model GetWorldModel( string id )
	{
		var path = Get( id )?.WorldModel;
		if ( string.IsNullOrEmpty( path ) ) return null;

		_modelCache ??= new Dictionary<string, Model>( StringComparer.OrdinalIgnoreCase );

		if ( _modelCache.TryGetValue( path, out var cached ) && cached != null )
			return cached;

		var model = Model.Load( path );
		_modelCache[path] = model;
		return model;
	}
	
	public static string GetIcon( string id )
	{
		var path = Get( id )?.Icon;
		return string.IsNullOrEmpty( path ) ? null : path;
	}

	/// Fallback si pas d’icône dans l’asset (ex: test item)
	public static string GetIconOrFallback( string id )
	{
		var icon = GetIcon( id );
		if ( !string.IsNullOrEmpty( icon ) ) return icon;

		// fallback (tu peux changer)
		return "/ui/icons/item.png";
	}

	
	
	
}
