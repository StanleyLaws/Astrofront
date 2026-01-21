using Sandbox;

namespace Astrofront;

public static class AstrofrontCommands
{
    [ConCmd( "af_damage" )]
    public static void DamageSelf( int amount = 10 )
    {
        var player = Game.ActiveScene
            .GetAllComponents<PlayerState>()
            .FirstOrDefault( p => !p.IsProxy );

        if ( player == null )
        {
            Log.Warning( "[Astrofront] No local PlayerState found" );
            return;
        }

        AstrofrontHealthRules.ApplyDamage( player, amount );
    }
}
