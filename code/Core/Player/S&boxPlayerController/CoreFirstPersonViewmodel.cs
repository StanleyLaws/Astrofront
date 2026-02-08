using Sandbox;
using System;
using System.Linq;

namespace Astrofront;

public sealed class CoreFirstPersonViewmodel : Component, PlayerController.IEvents
{
	[Property] public string ArmsModelPath { get; set; } = "models/first_person/v_first_person_arms_human.vmdl_c";
	[Property] public bool ForceViewModelLayer { get; set; } = true;

	// Local offset in camera space (when parented to camera)
	[Property] public Vector3 ViewModelLocalOffset { get; set; } = new Vector3( 12f, 4f, -10f );
	[Property] public Angles ViewModelLocalAnglesOffset { get; set; } = new Angles( 0f, 0f, 0f );

	// Optional: ensure viewmodel isn't clipped by near plane
	[Property] public bool ForceSmallZNearInFps { get; set; } = true;
	[Property] public float FpsZNear { get; set; } = 1.5f;

	private const string ViewModelTag = "viewmodel";

	private PlayerController _pc;

	private GameObject _vmRoot;
	private SkinnedModelRenderer _vmRenderer;

	// Cache main cam as fallback
	private CameraComponent _cachedMainCam;
	private float _nextCamSearchTime;

	// Camera tag changes (restore on exit)
	private CameraComponent _lastCamTouched;
	private bool _addedViewmodelToRenderTags;
	private bool _removedViewmodelFromExcludeTags;

	protected override void OnEnabled()
	{
		if ( IsProxy ) return;

		_pc = GameObject.Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants );
		if ( _pc == null )
		{
			Log.Warning( "[CoreFirstPersonViewmodel] PlayerController introuvable." );
			return;
		}

		EnsureViewModelObjects();
		UpdateVisibilityAndParent();
	}

	protected override void OnDisabled()
	{
		if ( IsProxy ) return;

		RestoreCameraTagOverrides();

		if ( _vmRoot != null )
			_vmRoot.Enabled = false;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		if ( _pc == null )
			_pc = GameObject.Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants );

		if ( _pc == null ) return;

		EnsureViewModelObjects();
		UpdateVisibilityAndParent();
		TryApplyViewModelLayer();
	}

	// Called by PlayerController
	public void PostCameraSetup( CameraComponent cam )
	{
		if ( IsProxy ) return;
		if ( _pc == null || cam == null ) return;

		EnsureViewModelObjects();

		if ( _pc.ThirdPerson )
		{
			RestoreCameraTagOverrides();
			return;
		}

		// Best path: parent to THIS camera
		ParentToCamera( cam );

		// Make sure camera isn't filtering us out
		ApplyCameraTagOverrides( cam );

		if ( ForceSmallZNearInFps )
			cam.ZNear = FpsZNear;
	}

	private void EnsureViewModelObjects()
	{
		if ( _vmRoot == null || !_vmRoot.IsValid() )
		{
			_vmRoot = Scene.CreateObject();
			_vmRoot.Name = "fp_viewmodel_root";
			_vmRoot.Tags.Add( ViewModelTag );
		}

		if ( _vmRenderer == null || !_vmRenderer.IsValid() )
		{
			_vmRenderer = _vmRoot.Components.GetOrCreate<SkinnedModelRenderer>();
			_vmRenderer.UseAnimGraph = true;
		}

		var desired = string.IsNullOrWhiteSpace( ArmsModelPath ) ? null : Model.Load( ArmsModelPath );
		if ( desired != null && _vmRenderer.Model != desired )
			_vmRenderer.Model = desired;

		// Render normally (Off = invisible)
		_vmRenderer.RenderType = ModelRenderer.ShadowRenderType.On;

		// Apply layer/flags when SceneObject becomes available
		TryApplyViewModelLayer();
	}

	private void UpdateVisibilityAndParent()
	{
		if ( _vmRoot == null ) return;

		var shouldShow = _pc != null && !_pc.ThirdPerson && !string.IsNullOrWhiteSpace( ArmsModelPath );
		_vmRoot.Enabled = shouldShow;

		if ( !shouldShow )
		{
			RestoreCameraTagOverrides();

			// When not in FPS, detach back to player (keeps scene tidy)
			if ( _vmRoot.Parent != GameObject )
				_vmRoot.SetParent( GameObject, false );

			return;
		}

		// FPS but PostCameraSetup might not have fired yet: fallback main camera parenting
		var cam = GetMainCamera();
		if ( cam != null )
		{
			ParentToCamera( cam );
			ApplyCameraTagOverrides( cam );
		}
	}

	private void ParentToCamera( CameraComponent cam )
	{
		if ( cam == null ) return;
		if ( _vmRoot == null ) return;

		var camGo = cam.GameObject;
		if ( camGo == null ) return;

		// Parent to camera
		if ( _vmRoot.Parent != camGo )
			_vmRoot.SetParent( camGo, false );

		// Local transform relative to camera
		_vmRoot.Transform.Local = new Transform(
			ViewModelLocalOffset,
			ViewModelLocalAnglesOffset.ToRotation()
		);

		TryApplyViewModelLayer();
	}

	private void TryApplyViewModelLayer()
	{
		if ( !ForceViewModelLayer ) return;
		if ( _vmRenderer == null || !_vmRenderer.IsValid() ) return;

		var so = _vmRenderer.SceneObject;
		if ( so == null ) return;

		// Ensure SceneObject is tagged too (camera filtering works on tags)
		if ( !so.Tags.Has( ViewModelTag ) )
			so.Tags.Add( ViewModelTag );

		// Mark as viewmodel render path
		so.RenderLayer = SceneRenderLayer.ViewModel;
		so.Flags.ViewModelLayer = true;

		// Typical for viewmodels: don't cast world shadows
		so.Flags.CastShadows = false;
	}

	private void ApplyCameraTagOverrides( CameraComponent cam )
	{
		if ( cam == null ) return;

		// If we changed a different camera before, restore it first
		if ( _lastCamTouched != null && _lastCamTouched.IsValid() && _lastCamTouched != cam )
			RestoreCameraTagOverrides();

		_lastCamTouched = cam;

		// If RenderTags isn't empty, only tagged objects render => ensure "viewmodel" is included
		if ( !cam.RenderTags.IsEmpty && !cam.RenderTags.Has( ViewModelTag ) )
		{
			cam.RenderTags.Add( ViewModelTag );
			_addedViewmodelToRenderTags = true;
		}

		// Ensure not excluded
		if ( cam.RenderExcludeTags.Has( ViewModelTag ) )
		{
			cam.RenderExcludeTags.Remove( ViewModelTag );
			_removedViewmodelFromExcludeTags = true;
		}
	}

	private void RestoreCameraTagOverrides()
	{
		if ( _lastCamTouched == null || !_lastCamTouched.IsValid() )
		{
			_lastCamTouched = null;
			_addedViewmodelToRenderTags = false;
			_removedViewmodelFromExcludeTags = false;
			return;
		}

		// Only revert what we changed
		if ( _addedViewmodelToRenderTags && _lastCamTouched.RenderTags.Has( ViewModelTag ) )
			_lastCamTouched.RenderTags.Remove( ViewModelTag );

		if ( _removedViewmodelFromExcludeTags && !_lastCamTouched.RenderExcludeTags.Has( ViewModelTag ) )
			_lastCamTouched.RenderExcludeTags.Add( ViewModelTag );

		_lastCamTouched = null;
		_addedViewmodelToRenderTags = false;
		_removedViewmodelFromExcludeTags = false;
	}

	private CameraComponent GetMainCamera()
	{
		if ( Time.Now < _nextCamSearchTime && _cachedMainCam != null && _cachedMainCam.IsValid() )
			return _cachedMainCam;

		_nextCamSearchTime = Time.Now + 0.25f;

		var cams = Scene?.GetAllComponents<CameraComponent>();
		if ( cams == null )
			return _cachedMainCam;

		_cachedMainCam = cams.FirstOrDefault( c => c != null && c.IsMainCamera );
		return _cachedMainCam;
	}
}
