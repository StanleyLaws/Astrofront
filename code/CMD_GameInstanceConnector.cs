using Sandbox;
using Sandbox.Network;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Astrofront
{
  /// Trouve une instance "Game" disponible et s'y connecte.
  public static class GameInstanceConnector
  {
    /// Essaie de se connecter à la première instance dont le nom commence par `AF_Game_`
    public static async Task<bool> ConnectToAvailableGame( string prefix = "AF_1x1_" )
    {
      try
      {
        Log.Info("[GameConnector] Query lobbies…");
        var lobbies = await Networking.QueryLobbies(); // List<LobbyInformation>
        if ( lobbies == null || lobbies.Count == 0 )
        {
          Log.Warning("[GameConnector] Aucun lobby retourné.");
          return false;
        }

        // ⚠️ PAS de test 'l != null' : LobbyInformation est une struct
        var candidates = lobbies
          .Where( l =>
                 !string.IsNullOrWhiteSpace( l.Name )
              && l.Name.StartsWith( prefix, System.StringComparison.OrdinalIgnoreCase )
              && l.Members < l.MaxMembers )
          .OrderBy( l => l.Members )
          .ToList();

        if ( candidates.Count == 0 )
        {
          Log.Warning($"[GameConnector] Aucune instance correspondant à '{prefix}' avec slot libre.");
          return false;
        }

        var target = candidates.First();
        Log.Info($"[GameConnector] Candidat: {target.Name} ({target.Members}/{target.MaxMembers}) id={target.LobbyId}");

        // Si on est déjà connecté (au Lobby), on se déconnecte proprement
        if ( Networking.IsActive )
        {
          Log.Info("[GameConnector] Disconnect (quitte le Lobby actuel) …");
          Networking.Disconnect();
          await GameTask.DelayRealtimeSeconds( 0.2f );
        }

        Log.Info($"[GameConnector] Connect -> {target.LobbyId}");
        Networking.Connect( target.LobbyId );

        // Attendre l’établissement de la session (max ~5s)
        var t0 = RealTime.Now;
        while ( !Networking.IsActive && RealTime.Now - t0 < 5f )
          await GameTask.DelayRealtimeSeconds( 0.05f );

        bool ok = Networking.IsActive;
        Log.Info( ok ? "[GameConnector] Connect OK" : "[GameConnector] Connect TIMEOUT" );
        return ok;
      }
      catch ( System.Exception e )
      {
        Log.Error( $"[GameConnector] Error: {e.Message}" );
        return false;
      }
    }

    /// Commande console pour tester rapidement depuis le client (menu ou lobby).
    [ConCmd( "af_join_game" )]
    public static async void CmdJoinGame()
    {
      _ = await ConnectToAvailableGame( "AF_1x1_" );
    }
  }
}
