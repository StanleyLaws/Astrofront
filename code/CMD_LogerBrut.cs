using Sandbox;
using Sandbox.Network;
using System;
using System.Threading.Tasks;

namespace Astrofront;

public static class LobbiesQuickCmds
{
    // Crée un lobby PUBLIC côté client (test local)
    [ConCmd( "astro_host_test" )]
    public static async void HostTest()
    {
        Log.Info("[HostTest] Creating local PUBLIC lobby…");
        Networking.CreateLobby( new LobbyConfig {
            Name = "AF ClientHostTest",
            Privacy = LobbyPrivacy.Public,
            MaxPlayers = 4
        } );

        // petit délai (1s) pour laisser le backend l’enregistrer
        await Task.Delay(1000);

        Log.Info("[HostTest] Done. Now run: astro_dump_lobbies");
    }

    // Liste brute des lobbies (debug)
    [ConCmd( "astro_dump_lobbies" )]
    public static async void DumpLobbies()
    {
        try
        {
            Log.Info("[LobbiesDebug] Querying…");
            var list = await Networking.QueryLobbies();
            var count = list?.Count ?? 0;
            Log.Info($"[LobbiesDebug] Count = {count}");

            if ( list != null )
            {
                foreach ( var l in list )
                {
                    Log.Info($"[LobbiesDebug] {l.Name} | id={l.LobbyId} | {l.Members}/{l.MaxMembers}");
                }
            }
        }
        catch ( Exception e )
        {
            Log.Warning($"[LobbiesDebug] failed: {e.Message}");
        }
    }
}
