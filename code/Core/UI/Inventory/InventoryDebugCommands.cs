using Sandbox;
using System;
using System.Linq;

namespace Astrofront;

public static class InventoryDebugCommands
{
    [ConCmd]
    public static void af_give_res( int amount, string resourceType )
    {
        var caller = Connection.Local; // <-- la seule source fiable en Net10

        if ( caller == null )
        {
            Log.Warning( "[af_give_res] Aucun joueur local trouvé." );
            return;
        }

        if ( amount <= 0 )
        {
            Log.Warning( "[af_give_res] amount doit être > 0." );
            return;
        }

        if ( !Enum.TryParse<ResourceType>( resourceType, true, out var type ) )
        {
            Log.Warning( $"[af_give_res] Type inconnu : {resourceType}" );
            return;
        }

        var scene = Game.ActiveScene;
        if ( scene == null )
        {
            Log.Warning( "[af_give_res] Aucune scène active." );
            return;
        }

        // Trouver le PlayerState du joueur local
        var ps = scene.GetAllComponents<PlayerState>()
                      ?.FirstOrDefault( p => p != null 
                                          && p.Network != null
                                          && p.Network.Owner == caller );

        if ( ps == null )
        {
            Log.Warning( "[af_give_res] Aucun PlayerState trouvé pour ce joueur." );
            return;
        }

        var inv = ps.GameObject.Components.Get<InventorySystem>( FindMode.EverythingInSelfAndDescendants );

        if ( inv == null )
        {
            Log.Warning( "[af_give_res] Aucun InventorySystem trouvé." );
            return;
        }

        // Don capacity-aware
        int accepted = inv.AddResourceServerReturnAccepted( caller, type, amount );

        if ( accepted <= 0 )
        {
            Log.Info( $"[af_give_res] Inventaire plein, aucune ressource ajoutée ({type})." );
            return;
        }

        Log.Info( $"[af_give_res] Ajouté {accepted} x {type} au joueur." );
    }
}
