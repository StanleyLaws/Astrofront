using Sandbox;
using Sandbox.Network; // LobbyConfig, LobbyPrivacy

namespace Astrofront;

public sealed class AutoListen : Component
{
    protected override void OnStart()
    {
        if ( !Networking.IsActive )
        {
            Networking.CreateLobby( new LobbyConfig
            {
                MaxPlayers = 8, 
                Privacy = LobbyPrivacy.Private,
                Name = "Local Dev"
            } );
            Log.Info("[AutoListen] CreateLobby() – host prêt");
        }
    }
}
