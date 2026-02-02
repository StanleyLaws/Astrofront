using Sandbox;
using System.Linq; // si pas déjà présent

namespace Astrofront;

public sealed class GameNetworkManagerAF : Component, Component.INetworkListener
{
    [Property, Title("Player Prefab")]
    public GameObject PlayerPrefab { get; set; }

    [Property, Title("Spectator Prefab")]
    public GameObject SpectatorPrefab { get; set; } // ← NEW

    [Property, Title("Spawn Points")]
    public GameObject[] SpawnPoints { get; set; }

    public void OnConnected( Connection connection ) { }

    public void OnActive( Connection connection )
    {
        if ( PlayerPrefab == null )
        {
            Log.Error("[GameNetworkManager] PlayerPrefab n'est pas assigné.");
            return;
        }

        var mm = Scene.GetAllComponents<MatchManager>().FirstOrDefault();
        if ( mm != null && mm.IsSpectator( connection ) )
        {
            // ---- SPAWN SPECTATEUR ----
            if ( SpectatorPrefab != null )
            {
                var spec = SpectatorPrefab.Clone( Transform.World ); // position par défaut
                spec.NetworkSpawn( connection );
                ChatSystem.ReceiveSystemMessage( $"{connection.DisplayName} a rejoint en spectateur." );
                Log.Info($"[GameNetworkManager] Spawned SPECTATOR for {connection.DisplayName}");
            }
            else
            {
                // Si pas de prefab défini, on log juste (provisoirement)
                ChatSystem.ReceiveSystemMessage( $"{connection.DisplayName} en spectateur (aucun prefab défini)." );
                Log.Warning("[GameNetworkManager] SpectatorPrefab n'est pas assigné.");
            }
            return;
        }

        // ---- SPAWN JOUEUR ----
        Transform tr = Transform.World;
        if ( SpawnPoints != null && SpawnPoints.Length > 0 )
        {
            var sp = SpawnPoints[Game.Random.Int(0, SpawnPoints.Length - 1)];
            if ( sp != null ) tr = sp.Transform.World;
        }
 
        var player = PlayerPrefab.Clone( tr );
        player.NetworkSpawn( connection );
		Astrofront_Rules.ApplyHost( player );
		Astrofront_SboxController_Rules.ApplyLocal( player );

        Log.Info($"[GameNetworkManager] Spawned player for {connection.DisplayName}");
    }

    public void OnDisconnected( Connection connection ) { }
}
