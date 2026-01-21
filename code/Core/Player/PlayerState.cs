// /code/PlayerState.cs
using Sandbox;
using System;
using System.Linq;

namespace Astrofront;

public enum Team { None = -1, Red = 0, Blue = 1 }

/// <summary>
/// État et logique locale de base d’un joueur.
/// - Les variables d’état (Team/Health/Kills) sont synchronisées depuis le host.
/// - La caméra et les inputs ne s’activent QUE pour le propriétaire local (pas pour les proxys).
/// </summary>
public sealed class PlayerState : Component 
{
    // ====== État réseau (sync depuis le host) ======
    [Sync( SyncFlags.FromHost )] public Team Team   { get; private set; } = Team.None;
    [Sync( SyncFlags.FromHost )] public int  Health { get; private set; } = 100;
    [Sync( SyncFlags.FromHost )] public int  Kills  { get; private set; }

    // ====== Réfs utiles ======
    private CameraComponent _camera;

    // ====== Cycle de vie local ======
    protected override void OnStart()
    {
		if ( !IsProxy ) GameObject.Tags.Add( "localplayer" );
		
		
        // Récupère la caméra enfant du prefab (si présente)
        _camera = Components.Get<CameraComponent>( FindMode.EverythingInSelfAndDescendants );

        // Active la caméra UNIQUEMENT pour le propriétaire local
        if ( _camera != null )
            _camera.Enabled = !IsProxy;
    }

    protected override void OnUpdate()
    {
        // Rien de local (inputs/caméra/FX) ne doit tourner sur un proxy
        if ( IsProxy ) return;
		
		// NEW: verrou d'UI (empêche WASD, use, etc.)
		if ( LootController.IsUiLockedLocal )
			return;

        // Ici tu peux mettre tes contrôles locaux (ex. déplacement, tir, etc.)
        // Ex minimal : appuyer sur R pour se "soigner" côté owner puis demander au host d'appliquer une TP de test
        // (juste un exemple – à retirer dans ton vrai gameplay)
        if ( Input.Pressed( "Reload" ) )
        {
            // Démo: simple heal local (non network-auth), à ne pas garder en prod
            Health = Math.Clamp( Health + 10, 0, 100 );
        }
    }

    // ====== API côté host ======

    /// <summary>Appelé côté host pour fixer l’équipe.</summary>
    public void SetTeamHost( Team t )
    {
        if ( !Networking.IsHost ) return;
        Team = t;
    }

    /// <summary>Applique des dégâts, autorité host.</summary>
    [Rpc.Host]
    public void ApplyDamage( int amount, Guid attackerId )
    {
        var nh = Health - amount;
        Health = nh < 0 ? 0 : nh;

        if ( Health == 0 )
        {
            Respawn();
        }
    }

    /// <summary>Respawn basique (remet la vie et efface l’interpolation pour éviter l’effet caoutchouc).</summary>
    void Respawn()
	{
		Health = 100;
		Network.ClearInterpolation();

		var mm = Scene?.GetAllComponents<MatchManager>().FirstOrDefault();
		if ( mm == null ) return;

		GameObject spawn = null;
		if ( Team == Team.Red && mm.RedSpawns is { Length: > 0 } )
			spawn = mm.RedSpawns[Game.Random.Int(0, mm.RedSpawns.Length - 1)];
		else if ( Team == Team.Blue && mm.BlueSpawns is { Length: > 0 } )
			spawn = mm.BlueSpawns[Game.Random.Int(0, mm.BlueSpawns.Length - 1)];

		if ( spawn == null ) return;

		var pos = spawn.Transform.World.Position;
		var rot = spawn.Transform.World.Rotation;

		// 1) côté serveur (source de vérité)
		GameObject.Transform.World = new Transform( pos, rot );

		// 2) côté owner (visuel fluide chez le joueur)
		TeleportOwner( pos, rot );
	}

    // ====== API côté owner ====== 

    /// <summary>TP côté propriétaire (utile quand le host décide d’un nouveau point et veut forcer la position locale).</summary>
    [Rpc.Owner]
    public void TeleportOwner( Vector3 pos, Rotation rot )
    {
        GameObject.Transform.World = new Transform( pos, rot );
        Network.ClearInterpolation();
    }
}
