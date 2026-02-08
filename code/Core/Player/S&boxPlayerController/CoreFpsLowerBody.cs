using System;
using System.Collections.Generic;
using Sandbox;

namespace Astrofront;

public sealed class CoreFpsLowerBody : Component, PlayerController.IEvents
{
	// ----------------------------
	// Visible wardrobe selection in FPS
	// ----------------------------

	[Property] public string[] FpsVisibleLowerKeywords { get; set; } =
	{
		"pants", "trousers", "jeans", "shoe", "shoes", "boot", "boots", "feet", "foot",
		"legs", "leg"
	};

	/// <summary>Si coché, affiche aussi le torse en FPS (vêtements supérieurs).</summary>
	[Property] public bool ShowTorsoInFps { get; set; } = false;

	[Property] public string[] FpsVisibleTorsoKeywords { get; set; } =
	{
		"shirt", "jacket", "hood", "hoodie", "torso", "upper", "coat", "vest", "sweater",
		"chest"
	};

	// ----------------------------
	// Camera anchor (FPS only)
	// ----------------------------

	/// <summary>
	/// Si activé, on ancre la caméra sur un attachment (ex: "eyes") ou un bone (ex: "head").
	/// Ça règle le problème "la caméra ne suit pas la tête en course".
	/// </summary>
	[Property] public bool EnableFpsCameraAnchor { get; set; } = true;

	/// <summary>
	/// Priorité #1: attachment. Mets "eyes" si ton modèle a un attachment eyes.
	/// Laisse vide si tu veux passer direct au bone.
	/// </summary>
	[Property] public string FpsCameraAnchorAttachment { get; set; } = "eyes";

	/// <summary>
	/// Fallback: bone name, souvent "head".
	/// </summary>
	[Property] public string FpsCameraAnchorBone { get; set; } = "head";

	/// <summary>
	/// Offset appliqué après l'ancrage.
	/// IMPORTANT: limiter X (avant) et jouer sur Z + ZNear.
	/// </summary>
	[Property] public Vector3 FpsCameraOffsetYawSpace { get; set; } = new Vector3( 40f, 0f, 12f );

	[Property] public float CameraTraceRadius { get; set; } = 4f;

	// ----------------------------
	// FOV / Near (FPS only)
	// ----------------------------

	[Property] public bool EnableFpsFovOverride { get; set; } = true;
	[Property] public float FpsFieldOfView { get; set; } = 80f;

	[Property] public bool EnableFpsNearOverride { get; set; } = true;
	[Property] public float FpsZNear { get; set; } = 12f;

	/// <summary>
	/// Option utile: augmente un peu le near quand on affiche le torse (pour couper plus agressivement).
	/// Ça aide contre "je vois l'intérieur du modèle".
	/// </summary>
	[Property] public bool BoostNearWhenTorsoVisible { get; set; } = true;
	[Property] public float TorsoNearBoost { get; set; } = 6f;

	[Property] public bool DisableUseFovFromPreferencesInFps { get; set; } = true;

	// Optional smoothing (helps sprint mismatch)
	[Property] public bool EnableCameraSmoothing { get; set; } = true;
	[Property] public float CameraSmoothingTime { get; set; } = 0.06f;

	private PlayerController _pc;
	private SkinnedModelRenderer _skinned;

	private readonly List<ModelRenderer> _playerRenderers = new();
	private readonly Dictionary<ModelRenderer, ModelRenderer.ShadowRenderType> _originalRenderTypes = new();

	private bool _wasFirstPerson;

	// Save/restore PlayerController camera prefs flag
	private bool _savedUseFovFromPreferences;
	private bool _hasSavedUseFovFromPreferences;

	// Smoothed camera pos
	private bool _hasSmoothedPos;
	private Vector3 _smoothedPos;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( IsProxy )
			return;

		_pc = GameObject.Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants );
		if ( _pc == null )
		{
			Log.Warning( "[CoreFpsLowerBody] PlayerController introuvable (component doit être sur le même GO que le PlayerController)." );
			return;
		}

		// On prend le renderer skinned "principal" (celui qui porte les bones)
		_skinned = GameObject.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );

		if ( !_hasSavedUseFovFromPreferences )
		{
			_savedUseFovFromPreferences = _pc.UseFovFromPreferences;
			_hasSavedUseFovFromPreferences = true;
		}

		CachePlayerRenderers();

		_wasFirstPerson = !_pc.ThirdPerson;
		ApplyMode( force: true );
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();

		if ( IsProxy )
			return;

		RestoreRenderTypesSafe();
		RestorePcFlags();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( IsProxy || _pc == null )
			return;

		if ( _skinned == null )
			_skinned = GameObject.Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );

		CachePlayerRenderers();
		ApplyMode( force: false );

		if ( _pc.ThirdPerson )
			_hasSmoothedPos = false;
	}

	// ----------------------------
	// PlayerController.IEvents
	// ----------------------------

	public void PostCameraSetup( CameraComponent cam )
	{
		if ( IsProxy || _pc == null || cam == null )
			return;

		// FPS only
		if ( _pc.ThirdPerson )
			return;

		// 1) Determine base position
		var basePos = cam.GameObject.WorldPosition;

		if ( EnableFpsCameraAnchor && _skinned != null )
		{
			if ( TryGetAnchorWorldTransform( out var anchorTx ) )
			{
				basePos = anchorTx.Position;
			}
		}

		// 2) Apply yaw-only offset from that base
		var yawOnly = new Angles( 0f, _pc.EyeAngles.yaw, 0f );
		var yawRot = yawOnly.ToRotation();

		var desired = basePos + (yawRot * FpsCameraOffsetYawSpace);

		// 3) Prevent clipping into walls
		var tr = Scene.Trace
			.Ray( basePos, desired )
			.Radius( CameraTraceRadius )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		var finalPos = tr.Hit ? tr.EndPosition : desired;

		// 4) Smoothing (optional)
		if ( EnableCameraSmoothing )
		{
			if ( !_hasSmoothedPos )
			{
				_smoothedPos = finalPos;
				_hasSmoothedPos = true;
			}
			else
			{
				var dt = Time.Delta;
				var t = CameraSmoothingTime <= 0.0001f ? 1f : (1f - MathF.Exp( -dt / CameraSmoothingTime ));
				_smoothedPos = Vector3.Lerp( _smoothedPos, finalPos, t );
			}

			finalPos = _smoothedPos;
		}

		cam.GameObject.WorldPosition = finalPos;

		// 5) Override FOV / Near
		if ( EnableFpsFovOverride )
		{
			if ( DisableUseFovFromPreferencesInFps )
				_pc.UseFovFromPreferences = false;

			cam.FieldOfView = FpsFieldOfView;
		}

		if ( EnableFpsNearOverride )
		{
			var near = FpsZNear;

			// Extra cut if torso is visible (helps reduce seeing inside clothing/chest)
			if ( ShowTorsoInFps && BoostNearWhenTorsoVisible )
				near += TorsoNearBoost;

			cam.ZNear = near;
		}
	}

	private bool TryGetAnchorWorldTransform( out Transform tx )
	{
		tx = default;

		// Attachment priority (ex: "eyes")
		if ( !string.IsNullOrWhiteSpace( FpsCameraAnchorAttachment ) )
		{
			var at = _skinned.GetAttachment( FpsCameraAnchorAttachment, worldSpace: true );
			if ( at.HasValue )
			{
				tx = at.Value;
				return true;
			}
		}

		// Bone fallback (ex: "head")
		if ( !string.IsNullOrWhiteSpace( FpsCameraAnchorBone ) )
		{
			if ( _skinned.TryGetBoneTransform( FpsCameraAnchorBone, out var boneTx ) )
			{
				tx = boneTx;
				return true;
			}
		}

		return false;
	}

	// ----------------------------
	// Rendering logic
	// ----------------------------

	private void CachePlayerRenderers()
	{
		_playerRenderers.Clear();

		var renderers = GameObject.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants );
		foreach ( var r in renderers )
		{
			if ( r == null ) continue;

			_playerRenderers.Add( r );

			if ( !_originalRenderTypes.ContainsKey( r ) )
				_originalRenderTypes[r] = r.RenderType;
		}
	}

	private void ApplyMode( bool force )
	{
		var isFirstPerson = !_pc.ThirdPerson;

		if ( !force && isFirstPerson == _wasFirstPerson )
			return;

		_wasFirstPerson = isFirstPerson;

		if ( isFirstPerson )
		{
			_pc.HideBodyInFirstPerson = false;

			// Everything => shadows only (keeps perfect shadow setup)
			foreach ( var r in _playerRenderers )
			{
				if ( r == null ) continue;
				r.RenderType = ModelRenderer.ShadowRenderType.ShadowsOnly;
			}

			// Lower visible
			foreach ( var r in _playerRenderers )
			{
				if ( r == null ) continue;
				if ( MatchesAnyKeyword( r, FpsVisibleLowerKeywords ) )
					r.RenderType = ModelRenderer.ShadowRenderType.On;
			}

			// Optional torso visible
			if ( ShowTorsoInFps )
			{
				foreach ( var r in _playerRenderers )
				{
					if ( r == null ) continue;
					if ( MatchesAnyKeyword( r, FpsVisibleTorsoKeywords ) )
						r.RenderType = ModelRenderer.ShadowRenderType.On;
				}
			}
		}
		else
		{
			RestoreRenderTypesSafe();
			RestorePcFlags();
		}
	}

	private static bool MatchesAnyKeyword( ModelRenderer r, string[] keywords )
	{
		if ( keywords == null || keywords.Length == 0 )
			return false;

		var goName = r.GameObject?.Name ?? string.Empty;

		var modelName = string.Empty;
		try { modelName = r.Model?.Name ?? string.Empty; } catch { }

		foreach ( var k in keywords )
		{
			if ( string.IsNullOrWhiteSpace( k ) ) continue;

			if ( goName.Contains( k, StringComparison.OrdinalIgnoreCase ) )
				return true;

			if ( modelName.Contains( k, StringComparison.OrdinalIgnoreCase ) )
				return true;
		}

		return false; 
	}

	private void RestoreRenderTypesSafe()
	{
		foreach ( var kv in _originalRenderTypes )
		{
			if ( kv.Key == null ) continue;
			kv.Key.RenderType = kv.Value;
		}
	}

	private void RestorePcFlags()
	{
		if ( _pc == null ) return;
		if ( !_hasSavedUseFovFromPreferences ) return;

		_pc.UseFovFromPreferences = _savedUseFovFromPreferences;
	}
}
