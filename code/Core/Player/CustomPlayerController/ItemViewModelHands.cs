using Sandbox;

namespace Astrofront;

[Group( "Astrofront" )]
[Title( "Item ViewModel Hands" )]
public sealed class ItemViewModelHands : Component
{
	// --------------------
	// Config
	// --------------------
	[Property, Group( "Models" )]
	public Model HandsModel { get; set; }

	[Property, Group( "Tuning" )]
	public Vector3 LocalOffset { get; set; } = Vector3.Zero;

	[Property, Group( "Tuning" )]
	public Angles LocalAngles { get; set; } = Angles.Zero;

	[Property, Group( "Tuning" )]
	public float CameraZNear { get; set; } = 1.0f;

	[Property, Group( "Tuning" )]
	public int ViewModelCameraPriority { get; set; } = 10;

	[Property, Group( "Tags" )]
	public string ViewModelTag { get; set; } = "viewmodel";

	[Property, Group( "Debug" )]
	public bool DebugLogs { get; set; } = true;

	// --------------------
	// Internal
	// --------------------
	private GameObject _ownerRoot;
	private MyCustomControllerCamera _ownerCamController;
	private CameraComponent _mainCamera;
	private CameraComponent _viewModelCamera;

	private GameObject _handsGO;
	private SkinnedModelRenderer _handsRenderer;

	protected override void OnEnabled()
	{
		if ( DebugLogs )
			Log.Info( $"[VMHands] OnEnabled item={GameObject?.Name} handsModel={(HandsModel != null)}" );

		if ( !IsLocalOwnerOrNoNetwork() )
		{
			if ( DebugLogs ) Log.Info( "[VMHands] Stop: not local owner" );
			return;
		}

		ResolveOwnerRefs();
		if ( _ownerRoot == null )
		{
			if ( DebugLogs ) Log.Info( "[VMHands] Stop: ownerRoot null" );
			return;
		}

		if ( _mainCamera == null )
		{
			if ( DebugLogs ) Log.Info( "[VMHands] Stop: mainCamera null" );
			return;
		}

		EnsureViewModelCamera();
		EnsureHandsObject();
		UpdateVisibilityAndPose();
	}

	protected override void OnDisabled()
	{
		if ( !IsLocalOwnerOrNoNetwork() ) return;

		// On ne détruit pas la ViewModelCamera (shared joueur),
		// seulement les mains créées par cet item.
		DestroyHands();
	}

	protected override void OnDestroy()
	{
		DestroyHands();
	}

	protected override void OnUpdate()
	{
		if ( !IsLocalOwnerOrNoNetwork() ) return;

		if ( _ownerRoot == null || _mainCamera == null )
			ResolveOwnerRefs();

		if ( _mainCamera == null ) return;

		EnsureViewModelCamera();
		EnsureHandsObject();
		UpdateVisibilityAndPose();
	}

	private void DestroyHands()
	{
		if ( _handsGO.IsValid() )
		{
			if ( DebugLogs ) Log.Info( $"[VMHands] Destroy hands '{_handsGO.Name}'" );
			_handsGO.Destroy();
		}

		_handsGO = null;
		_handsRenderer = null;
	}

	private bool IsLocalOwnerOrNoNetwork()
	{
		// Si l'objet item n'est pas networké, on autorise côté client.
		if ( Network == null )
			return true;

		return Network.Owner == Connection.Local;
	}

	private void ResolveOwnerRefs()
	{
		var ps = Components.Get<PlayerState>( FindMode.EverythingInSelfAndAncestors );
		_ownerRoot = ps?.GameObject ?? GameObject.Root;

		if ( _ownerRoot == null ) return;

		_mainCamera = _ownerRoot.Components.Get<CameraComponent>( FindMode.EverythingInSelfAndDescendants );
		_ownerCamController = _ownerRoot.Components.Get<MyCustomControllerCamera>( FindMode.EverythingInSelfAndDescendants );
	}

	private void EnsureViewModelCamera()
	{
		// Si déjà assignée, reconfigure (au cas où elle a été modifiée/ancienne)
		if ( _viewModelCamera.IsValid() )
		{
			ApplyViewModelCameraConfig( _viewModelCamera );
			return;
		}

		// Cherche une caméra existante sous le joueur
		var existing = FindExistingViewModelCamera( _ownerRoot );
		if ( existing.IsValid() )
		{
			_viewModelCamera = existing;
			ApplyViewModelCameraConfig( _viewModelCamera );

			if ( DebugLogs )
				Log.Info( "[VMHands] Reusing existing ViewModelCamera (reconfigured)" );

			return;
		}

		// Sinon crée une seule fois
		var camGO = new GameObject( true, "ViewModelCamera" );
		camGO.SetParent( _ownerRoot, false );
		camGO.NetworkMode = NetworkMode.Never;

		_viewModelCamera = camGO.Components.Create<CameraComponent>();
		ApplyViewModelCameraConfig( _viewModelCamera );

		if ( DebugLogs )
			Log.Info( "[VMHands] ViewModelCamera created (singleton per player)" );
	}

	private void ApplyViewModelCameraConfig( CameraComponent cam )
	{
		if ( cam == null ) return;

		// Assure qu'elle est active
		cam.Enabled = true;

		// Paramètres importants
		cam.ClearFlags = ClearFlags.Depth | ClearFlags.Stencil;
		cam.ZNear = CameraZNear;
		cam.Priority = ViewModelCameraPriority;
		cam.TargetEye = StereoTargetEye.None;

		// Assure que la caméra rend le tag viewmodel
		// (si la liste est vide ou a été modifiée, on le réajoute)
		bool has = false;
		foreach ( var ts in cam.RenderTags )
		{
			if ( ts.Contains( ViewModelTag ) ) { has = true; break; }
		}
		if ( !has )
			cam.RenderTags.Add( new TagSet() { ViewModelTag } );

		// Exclut les viewmodels de la caméra principale
		if ( _mainCamera != null && !_mainCamera.RenderExcludeTags.Contains( ViewModelTag ) )
			_mainCamera.RenderExcludeTags.Add( ViewModelTag );
	}

	private static CameraComponent FindExistingViewModelCamera( GameObject ownerRoot )
	{
		if ( ownerRoot == null ) return null;

		foreach ( var cam in ownerRoot.Components.GetAll<CameraComponent>( FindMode.EverythingInSelfAndDescendants ) )
		{
			if ( cam == null ) continue;
			if ( cam.GameObject == null ) continue;

			// On cherche un GO nommé comme notre singleton
			if ( cam.GameObject.Name != "ViewModelCamera" ) continue;

			return cam;
		}

		return null;
	}

	private void EnsureHandsObject()
	{
		if ( _handsGO.IsValid() ) return;

		if ( HandsModel == null )
		{
			if ( DebugLogs ) Log.Info( "[VMHands] HandsModel NULL" );
			return;
		}

		if ( !_viewModelCamera.IsValid() )
		{
			if ( DebugLogs ) Log.Info( "[VMHands] ViewModelCamera invalid" );
			return;
		}

		_handsGO = new GameObject( true, "ViewmodelHands" );
		_handsGO.SetParent( _viewModelCamera.GameObject, false );
		_handsGO.NetworkMode = NetworkMode.Never;
		_handsGO.Tags.Add( ViewModelTag );

		_handsRenderer = _handsGO.Components.Create<SkinnedModelRenderer>();
		_handsRenderer.Model = HandsModel;
		_handsRenderer.AnimationGraph = HandsModel.AnimGraph;

		if ( DebugLogs )
			Log.Info( $"[VMHands] Hands GO created. parent={_viewModelCamera.GameObject.Name} tag={ViewModelTag}" );
	}

	private void UpdateVisibilityAndPose()
	{
		if ( !_handsGO.IsValid() ) return;

		bool isFP =
			_ownerCamController != null &&
			_ownerCamController.Mode == MyCustomControllerCamera.CameraMode.FirstPerson;

		_handsGO.Enabled = isFP;

		_handsGO.LocalPosition = LocalOffset;
		_handsGO.LocalRotation = Rotation.From( LocalAngles );
	}
}
