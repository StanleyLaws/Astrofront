using Sandbox;
using System;
using System.Collections.Generic;

namespace Astrofront;

/// <summary>
/// Système d'énergie CENTRAL (Core) :
/// - 1 seul component sur le prefab (pas 1 component par "source d'énergie").
/// - Le HOST est la vérité : il applique drains/regen et écrit dans PlayerState.
/// - Les autres systèmes (sprint, jetpack, overweight, etc.) ne font que déclarer
///   des "drains actifs" (rate par seconde) ou des "coups instantanés".
///
/// Intention :
/// - Plusieurs drains peuvent être actifs en même temps (ex: sprint + jetpack).
/// - Certains modes peuvent désactiver l'énergie (MaxEnergy=0 ou Enabled=false).
/// - Energy est en int (cohérent avec PlayerState), mais les rates sont en float.
/// </summary>
public sealed class PlayerEnergySystem : Component
{
	/// <summary>
	/// Active/désactive le système (sync depuis le host).
	/// Note : si MaxEnergy == 0, le système se considère aussi "off".
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public bool EnergyEnabled { get; private set; } = true;


	/// <summary>
	/// Régénération passive (énergie par seconde) quand rien n'empêche la regen.
	/// Mettre 0 pour désactiver la regen passive.
	/// (configurable par Rules côté host)
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public float PassiveRegenPerSecond { get; private set; } = 0f;

	/// <summary>
	/// Si true, la regen passive continue même si des drains sont actifs.
	/// Si false, regen passive seulement quand drain total == 0.
	/// (souvent tu veux false : pas de regen pendant sprint)
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public bool RegenWhileDraining { get; private set; } = false;

	/// <summary>
	/// Plancher d'énergie (souvent 0).
	/// </summary>
	[Sync( SyncFlags.FromHost )]
	public int MinEnergy { get; private set; } = 0;

	// --- interne ---

	private PlayerState _ps;

	// Drains actifs : id -> rate (énergie / seconde)
	// Exemple d'id : "sprint", "jetpack", "overweight"
	private readonly Dictionary<string, float> _drains = new();

	// Accumulateur float pour convertir proprement en int (évite de perdre les petits dt)
	private float _accum;

	protected override void OnStart()
	{
		_ps = Components.Get<PlayerState>( FindMode.EverythingInSelfAndDescendants );
		if ( _ps == null )
			Log.Warning( "[PlayerEnergySystem] PlayerState introuvable : le système ne peut pas écrire l'énergie." );
	}

	protected override void OnFixedUpdate()
	{
		// Seul le host modifie la valeur d'énergie sync
		if ( !Networking.IsHost ) return;
		if ( _ps == null ) return;

		// Désactivation automatique si le mode ne veut pas d'énergie
		if ( !EnergyEnabled || _ps.MaxEnergy <= 0 )
		{
			_accum = 0f;
			return;
		}

		float totalDrain = 0f;
		foreach ( var kv in _drains )
		{
			if ( kv.Value > 0f )
				totalDrain += kv.Value;
		}

		// Regen passive (optionnelle)
		float regen = PassiveRegenPerSecond;
		bool allowRegen = regen > 0f && (RegenWhileDraining || totalDrain <= 0f);

		// delta énergie (float) pour cette frame
		float delta = 0f;

		// drains
		if ( totalDrain > 0f )
			delta -= totalDrain * Time.Delta;

		// regen
		if ( allowRegen )
			delta += regen * Time.Delta;

		if ( MathF.Abs( delta ) < 0.0001f )
			return;

		// Accumuler puis convertir en int par "ticks"
		_accum += delta;

		// Appliquer autant d'unités entières que possible
		int intDelta = (int)_accum;

		// Pour les valeurs négatives : (int) -0.2f == 0, donc on gère le floor
		if ( _accum < 0f )
			intDelta = (int)MathF.Floor( _accum );
		else
			intDelta = (int)MathF.Floor( _accum );

		if ( intDelta == 0 )
			return;

		_accum -= intDelta;

		// Appliquer sur PlayerState via son API host
		_ps.AddEnergyHost( intDelta );

		// Clamp min/max (PlayerState clamp déjà sur MaxEnergy, mais on assure MinEnergy)
		if ( _ps.Energy < MinEnergy )
			_ps.SetEnergyHost( MinEnergy );
	}

	// =========================================================
	// API HOST (appelée par Rules / gameplay serveur)
	// =========================================================

	/// <summary>
	/// Active/désactive le système (HOST only).
	/// Si désactivé, ça ne force pas Energy à 0 : ça stoppe juste la simulation.
	/// </summary>
	public void SetEnergyEnabledHost( bool enabled )
	{
		if ( !Networking.IsHost ) return;

		EnergyEnabled = enabled;

		if ( !EnergyEnabled )
		{
			_drains.Clear();
			_accum = 0f;
		}
	}


	/// <summary>
	/// Configure la regen passive (HOST only).
	/// </summary>
	public void SetPassiveRegenHost( float perSecond, bool regenWhileDraining = false )
	{
		if ( !Networking.IsHost ) return;
		PassiveRegenPerSecond = MathF.Max( 0f, perSecond );
		RegenWhileDraining = regenWhileDraining;
	}

	/// <summary>
	/// Configure le minimum d'énergie (HOST only).
	/// </summary>
	public void SetMinEnergyHost( int minEnergy )
	{
		if ( !Networking.IsHost ) return;
		MinEnergy = Math.Max( 0, minEnergy );
	}

	/// <summary>
	/// Applique un changement instantané (HOST only).
	/// Ex: dash coûte 10 d'énergie => AddInstantHost(-10, "dash")
	/// </summary>
	public void AddInstantHost( int amount, string reason = "" )
	{
		if ( !Networking.IsHost ) return;
		if ( _ps == null ) return;
		if ( !Enabled || _ps.MaxEnergy <= 0 ) return;

		_ps.AddEnergyHost( amount );
		if ( _ps.Energy < MinEnergy )
			_ps.SetEnergyHost( MinEnergy );

		// reason est volontairement unused (tu peux log plus tard si besoin)
	}

	/// <summary>
	/// Déclare/maj un drain (HOST only) : rate en énergie/seconde.
	/// Mettre rate<=0 pour retirer le drain.
	/// </summary>
	public void SetDrainHost( string id, float ratePerSecond )
	{
		if ( !Networking.IsHost ) return;
		if ( string.IsNullOrWhiteSpace( id ) ) return;

		if ( ratePerSecond <= 0f )
			_drains.Remove( id );
		else
			_drains[id] = ratePerSecond;
	}

	/// <summary>
	/// Supprime un drain (HOST only).
	/// </summary>
	public void ClearDrainHost( string id )
	{
		if ( !Networking.IsHost ) return;
		if ( string.IsNullOrWhiteSpace( id ) ) return;
		_drains.Remove( id );
	}

	/// <summary>
	/// Nettoie tous les drains (HOST only).
	/// </summary>
	public void ClearAllDrainsHost()
	{
		if ( !Networking.IsHost ) return;
		_drains.Clear();
		_accum = 0f;
	}

	// =========================================================
	// API CLIENT -> HOST (pour les intentions locales)
	// =========================================================
	// But : un système local (ex: sprint) peut demander au host d'activer un drain.
	// Le host reste la vérité : il applique la valeur réelle dans PlayerState.

	/// <summary>
	/// Côté owner/local : demande au host de set un drain.
	/// (Sécurité anti-cheat : plus tard tu pourras filtrer/valider côté host).
	/// </summary>
	public void SetDrainLocal( string id, float ratePerSecond )
	{
		if ( IsProxy ) return;
		SetDrainHostRpc( id, ratePerSecond );
	}

	/// <summary>
	/// Côté owner/local : demande au host d'ajouter un coût instantané.
	/// </summary>
	public void AddInstantLocal( int amount, string reason = "" )
	{
		if ( IsProxy ) return;
		AddInstantHostRpc( amount, reason );
	}

	[Rpc.Host]
	private void SetDrainHostRpc( string id, float ratePerSecond )
	{
		SetDrainHost( id, ratePerSecond );
	}

	[Rpc.Host]
	private void AddInstantHostRpc( int amount, string reason )
	{
		AddInstantHost( amount, reason );
	}

	// =========================================================
	// Helpers (lecture)
	// =========================================================

	/// <summary>Total drain actuel (host-only utile pour debug).</summary>
	public float GetTotalDrainHost()
	{
		if ( !Networking.IsHost ) return 0f;

		float total = 0f;
		foreach ( var kv in _drains )
			if ( kv.Value > 0f ) total += kv.Value;

		return total;
	}
}
