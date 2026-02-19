using Sandbox;
using System.Collections.Generic;

namespace Astrofront;

/// <summary>
/// Gestion propre de la visibilité du corps en FirstPerson pour le joueur local:
/// - FP : Body => ShadowsOnly (invisible mais ombres animées)
/// - TP : restore l'état d'origine
/// Appliqué en OnPreRender pour battre les scripts qui réécrivent le rendu (CitizenAnimDriver/Dresser).
/// </summary>
[Group( "Astrofront" )]
[Title( "Local First Person Body Visibility" )]
public sealed class LocalFirstPersonBodyVisibility : Component
{
	[Property, Group( "Refs" )] public GameObject BodyObject { get; set; }
	[Property, Group( "Refs" )] public MyCustomControllerCamera CameraController { get; set; }

	[Property, Group( "Tags" )] public string ViewModelTag { get; set; } = "viewmodel";

	// Pour extension future: legs séparés
	[Property, Group( "Optional Legs" )] public GameObject LegsObject { get; set; }

	private readonly Dictionary<ModelRenderer, ModelRenderer.ShadowRenderType> _original = new();

	protected override void OnStart()
	{
		// fallback auto
		BodyObject ??= GameObject.Children.FirstOrDefault( c => c.Name == "Body" ) ?? GameObject;
		CameraController ??= GameObject.Components.Get<MyCustomControllerCamera>( FindMode.EverythingInSelfAndDescendants );

		CacheOriginalBodyRenderTypes();
	}

	protected override void OnPreRender()
	{
		// Only local player (même logique que tes tags localplayer)
		if ( IsProxy ) return;

		bool isFP = CameraController != null
			&& CameraController.Mode == MyCustomControllerCamera.CameraMode.FirstPerson;

		ApplyBodyMode( isFP );
		ApplyLegsMode( isFP );
	}

	private void CacheOriginalBodyRenderTypes()
	{
		_original.Clear();

		if ( BodyObject == null ) return;

		foreach ( var r in BodyObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( r == null || r.GameObject == null ) continue;

			// Ne jamais toucher aux viewmodels
			if ( r.GameObject.Tags.Has( ViewModelTag ) ) continue;
			if ( IsUnderViewModelCamera( r.GameObject ) ) continue;

			if ( !_original.ContainsKey( r ) )
				_original.Add( r, r.RenderType );
		}
	}

	private void ApplyBodyMode( bool isFP )
	{
		if ( BodyObject == null ) return;

		foreach ( var r in BodyObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( r == null || r.GameObject == null ) continue;

			if ( r.GameObject.Tags.Has( ViewModelTag ) ) continue;
			if ( IsUnderViewModelCamera( r.GameObject ) ) continue;

			if ( isFP )
			{
				r.RenderType = ModelRenderer.ShadowRenderType.ShadowsOnly;
				r.Enabled = true;
			}
			else
			{
				if ( _original.TryGetValue( r, out var rt ) )
					r.RenderType = rt;
				else
					r.RenderType = ModelRenderer.ShadowRenderType.On;

				r.Enabled = true;
			}
		}
	}

	private void ApplyLegsMode( bool isFP )
	{
		// Si tu ajoutes un LegsObject séparé (SkinnedModelRenderer legs),
		// alors: FP => visible, TP => invisible
		if ( LegsObject == null ) return;

		foreach ( var r in LegsObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( r == null ) continue;
			r.Enabled = isFP;
			r.RenderType = ModelRenderer.ShadowRenderType.On;
		}
	}

	private static bool IsUnderViewModelCamera( GameObject go )
	{
		var cur = go;
		while ( cur != null )
		{
			if ( cur.Name == "ViewModelCamera" )
				return true;

			cur = cur.Parent;
		}
		return false;
	}
}
