using Sandbox;
using Sandbox.Network;

namespace Astrofront;

/// À poser 1x dans chaque scène serveur (lobby.scene, game.scene, etc.).
/// Si aucune session réseau n'est active, crée automatiquement un lobby/serveur.
/// ENTIEREMENT whitelist-safe : pas de System.IO, pas de Reflection.
public sealed class AutoHostSession : Component
{
    // Backups éditables dans l’éditeur
    [Property, Title("Server Name")] public string Hostname { get; set; } = "AF_1x1_01";
    [Property] public int  MaxPlayers { get; set; } = 12;
    [Property] public bool Public     { get; set; } = true;

    // Overrides via ConVars (+astro_hostname, +astro_public, +astro_max)
    [ConVar("astro_hostname")] public static string CvHostname { get; set; } = "";
    [ConVar("astro_public")]   public static bool   CvPublic   { get; set; } = true;
    [ConVar("astro_max")]      public static int    CvMax      { get; set; } = 0;

    protected override void OnStart()
    {
        // Si on arrive ici via une connexion existante (client/host), ne rien créer
        if ( Networking.IsActive )
        {
            Log.Info("[AutoHostSession] Networking.IsActive == true → pas de création");
            return;
        }

        // ConVars > propriétés
        var name = !string.IsNullOrWhiteSpace(CvHostname) ? CvHostname : Hostname;
        var pub  = CvPublic;
        var max  = CvMax > 0 ? CvMax : MaxPlayers;

        Networking.CreateLobby( new LobbyConfig
        {
            Name       = name,
            Privacy    = pub ? LobbyPrivacy.Public : LobbyPrivacy.Private,
            MaxPlayers = max
        } );

        Log.Info($"[AutoHostSession] CreateLobby → Name='{name}', Public={pub}, Max={max}");
        // NOTE: pas de Lobby.SetData ici (API non whitelisted sur ta branche).
    }
}
