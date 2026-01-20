using Sandbox;

namespace Astrofront;

/// Gère le spawn des joueurs quand une connexion devient active (host + clients).
public sealed class GameNetworkManager : Component, Component.INetworkListener
{
    [Property, Title("Player Prefab")]
    public GameObject PlayerPrefab { get; set; }

    [Property, Title("Spawn Points")]
    public GameObject[] SpawnPoints { get; set; }

    public void OnConnected( Connection connection ) { }

    // Appelé quand une connexion devient active (y compris l'hôte lui-même)
    public void OnActive( Connection connection )
    {
        if ( PlayerPrefab == null )
        {
            Log.Error("[GameNetworkManager] PlayerPrefab n'est pas assigné.");
            return;
        }

        // Choisir un transform de spawn (ou utiliser celui du manager à défaut)
        Transform tr = Transform.World;
        if ( SpawnPoints != null && SpawnPoints.Length > 0 )
        {
            var sp = SpawnPoints[Game.Random.Int(0, SpawnPoints.Length - 1)];
            if ( sp != null ) tr = sp.Transform.World;
        }

        var player = PlayerPrefab.Clone( tr );
        player.NetworkSpawn( connection );

        Log.Info($"[GameNetworkManager] Spawned player for {connection.DisplayName}");
    }

    public void OnDisconnected( Connection connection ) { }
}
