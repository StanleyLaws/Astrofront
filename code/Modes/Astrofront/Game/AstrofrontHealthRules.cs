using Sandbox;
using System.Linq;

namespace Astrofront;

public sealed class AstrofrontHealthRules : Component
{
    /// <summary>
    /// Applique des dégâts à un joueur (HOST ONLY)
    /// </summary>
    public static void ApplyDamage( PlayerState target, int amount )
    {
        if ( target == null ) return;
        if ( !Networking.IsHost ) return;
        if ( !target.IsAlive ) return;

        target.AddHealthHost( -amount );

        if ( target.Health <= 0 )
        {
            KillPlayer( target );
        }
    }

    /// <summary>
    /// Gère la mort d’un joueur selon les règles Astrofront
    /// </summary>
    private static void KillPlayer( PlayerState target )
    {
        Log.Info( $"[Astrofront] Player died: {target.GameObject.Name}" );

        // Respawn simple (test)
        RespawnPlayer( target );
    }

    /// <summary>
    /// Respawn Astrofront (HOST)
    /// </summary>
    private static void RespawnPlayer( PlayerState target )
    {
        if ( !Networking.IsHost ) return;

        // Pour l’instant : respawn sur n’importe quel SpawnPoint
        var spawn = target.Scene?
            .GetAllComponents<SpawnPoint>()
            .OrderBy( _ => Game.Random.Float() )
            .FirstOrDefault();

        if ( spawn == null )
        {
            Log.Warning( "[Astrofront] No SpawnPoint found!" );
            return;
        }

        target.SetHealthHost( target.MaxHealth );
        target.TeleportHost(
            spawn.Transform.World.Position,
            spawn.Transform.World.Rotation
        );
    }
}



