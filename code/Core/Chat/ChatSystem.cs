using Sandbox;
using System;
using System.Collections.Generic;

namespace Astrofront;

/// Système de chat réseau (séparé de l'UI Razor)
public sealed class ChatSystem : Component, Component.INetworkListener
{
    // Anti-spam simple (1 msg / 0.75 s)
    private static readonly Dictionary<Guid, TimeSince> _lastByCaller = new();
    private const float MinDelaySeconds = 0.75f;

    // Client -> Serveur : reçoit le texte d'un client
    [Rpc.Host]
    public static void SendMessageHost( string message )
    {
        var caller = Rpc.Caller;
        if ( caller is null ) return;

        // anti-spam
        var callerId = Rpc.CallerId;
        if ( !_lastByCaller.TryGetValue( callerId, out var since ) )
            _lastByCaller[callerId] = 10;
        if ( since < MinDelaySeconds )
            return;
        _lastByCaller[callerId] = 0;

        // nettoyage
        message = (message ?? string.Empty).Trim();
        if ( message.Length == 0 ) return;
        if ( message.Length > 200 ) message = message.Substring( 0, 200 );

        // --- commande /balance (PRÉCISEMENT ici, après le "nettoyage") ---
        if ( message.Equals("/balance", StringComparison.OrdinalIgnoreCase) )
        {
            ShowBalanceFor( caller ); // appel asynchrone (fire-and-forget)
            return; // ne pas diffuser le /balance comme message normal
        }
		
				// --- commande /rank <pseudo> <grade> ---
		if ( message.StartsWith("/rank ", StringComparison.OrdinalIgnoreCase) )
		{
			HandleRankCommand( caller, message );
			return;
		}


        BroadcastWithRank( caller, message );
		return;
    }

    // Serveur -> Tous : diffuse un message utilisateur
    [Rpc.Broadcast]
    public static void ReceiveMessageBroadcast( SteamId steamId, string author, string message, Color color )
    {
        // log côté hôte/serveur
        if ( Networking.IsHost )
            Log.Info( $"[CHAT] {author}: {message}" );

        // pousse dans l'UI (Razor)
        Chat.Instance?.AddMessage( steamId, author, message, color );
    }

    // Serveur -> Tous : message système pratique
    [Rpc.Broadcast]
    public static void ReceiveSystemMessage( string text )
    {
        if ( Networking.IsHost )
            Log.Info( $"[CHAT][SYSTEM] {text}" );

        Chat.Instance?.AddSystemMessage( text );
    }

    // --- Hooks réseau (si tu veux les messages join/leave, laisse ce component dans la scène) ---
    public void OnActive( Connection connection )
    {
        ReceiveSystemMessage( $"{connection.DisplayName} a rejoint le serveur." );
    }

    public void OnDisconnected( Connection connection )
    {
        ReceiveSystemMessage( $"{connection.DisplayName} a quitté le serveur." );
    }

    // ---------------------------
    //  AJOUT : /balance (ici) 
    // ---------------------------
    private static async void ShowBalanceFor( Connection conn )
    {
        try
        {
            var steamId = conn.SteamId.ToString();

            var st    = await BackendClient.GetBalanceAsync( steamId, "STARDUST" );
            var money = await BackendClient.GetBalanceAsync( steamId, "MONEY" );

            // Diffuse un message système (visible par tous). 
            // Si tu veux un message privé, dis-le et on adapte.
            ReceiveSystemMessage( $"{conn.DisplayName} — Balance ● STARDUST: {st.amount} ● MONEY: {money.amount}" );
        }
        catch ( Exception ex )
        {
            Log.Warning( $"[Chat]/balance erreur: {ex.Message}" );
            ReceiveSystemMessage( $"Impossible de récupérer la balance pour {conn.DisplayName}" );
        }
    }
	
	
	private static readonly string[] AllowedRanks = new[] { "ADMIN", "MODERATOR", "VIP", "PLAYER" };

	private static async void HandleRankCommand( Connection caller, string message )
	{
		try
		{
			// /rank <pseudo> <grade>
			var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if ( parts.Length < 3 )
			{
				ReceiveSystemMessage( "Usage: /rank <pseudo> <ADMIN|MODERATOR|VIP|PLAYER>" );
				return;
			}

			var targetName = parts[1];
			var rankUpper  = parts[2].Trim().ToUpperInvariant();

			// Validation du grade demandé
			if ( Array.IndexOf( AllowedRanks, rankUpper ) < 0 )
			{
				ReceiveSystemMessage( "Grade invalide. Autorisés: ADMIN, MODERATOR, VIP, PLAYER" );
				return;
			}

			// Vérifier le grade de l'appelant (ADMIN uniquement)
			var callerSteam = caller.SteamId.ToString();
			var callerRank  = (await BackendClient.GetRankAsync( callerSteam )).rank ?? "PLAYER";
			if ( callerRank != "ADMIN" )
			{
				ReceiveSystemMessage( $"{caller.DisplayName}: permission refusée (ADMIN requis)" );
				return;
			}


			// Trouver la cible par pseudo (exact ou début correspondant)
			var target = FindConnectionByName( targetName );
			if ( target is null )
			{
				ReceiveSystemMessage( $"Joueur introuvable: {targetName}" );
				return;
			}

			// Appliquer le grade via l’API
			var res = await BackendClient.SetRankAsync( target.SteamId.ToString(), rankUpper );

			// Feedback
			ReceiveSystemMessage( $"[RANK] {target.DisplayName} est maintenant {res.rank}" );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[Chat]/rank erreur: {ex.Message}" );
			ReceiveSystemMessage( "Erreur lors du changement de grade." );
		}
	}

	// Cherche d’abord correspondance exacte (insensible à la casse), puis par préfixe.
	private static Connection FindConnectionByName( string name )
	{
		if ( string.IsNullOrWhiteSpace( name ) ) return null;

		Connection exact = null;
		Connection starts = null;

		foreach ( var c in Connection.All )
		{
			var dn = c?.DisplayName ?? "";
			if ( dn.Equals( name, StringComparison.OrdinalIgnoreCase ) )
				exact = c;
			else if ( starts is null && dn.StartsWith( name, StringComparison.OrdinalIgnoreCase ) )
				starts = c;
		}

		return exact ?? starts;
	} 

	
	
	private static async void BroadcastWithRank( Connection caller, string message )
	{
		try
		{
			var steamId = caller.SteamId;
			var display = caller.DisplayName ?? "Player";

			// Lire le grade depuis le backend
			var rank = (await BackendClient.GetRankAsync( steamId.ToString() )).rank ?? "PLAYER";

			// Obtenir le tag et la couleur
			var (tag, color) = RankVisual( rank );

			// Préfixer le tag coloré au pseudo
			var author = $"{tag} {display}";

			// Diffuser (la couleur teinte la ligne selon le rank)
			ReceiveMessageBroadcast( steamId, author, message, color );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[CHAT] BroadcastWithRank erreur: {ex.Message}" );
			// fallback neutre
			ReceiveMessageBroadcast( caller.SteamId, caller.DisplayName ?? "Player", message, Color.White );
		}
	}

	private static (string tag, Color color) RankVisual( string rank )
	{
		switch ( (rank ?? "PLAYER").ToUpperInvariant() )
		{
			case "ADMIN":     return ("[ADMIN]",     Color.Red);
			case "MODERATOR": return ("[MODERATOR]", Color.Blue);
			case "VIP":       return ("[VIP]",       Color.Orange);
			default:          return ("[Joueur]",    Color.Green);
		}
	}

	
	
	

    // (optionnel) test via console
    [ConCmd( "say_test" )]
    public static void SayTest( string text ) => SendMessageHost( text );
	
	
		// Console (comme say_test) : changer un grade
	// Usage: cons_rank <pseudo_ou_steamid> <ADMIN|MODERATOR|VIP|PLAYER>
	[ConCmd( "cons_rank" )]
	public static void CmdRank( string target, string rank )
	{
		// Exécuter côté serveur uniquement
		if ( !Networking.IsHost ) return;

		_ = RankConsoleSet( target, rank ); // fire-and-forget
	}

	private static async System.Threading.Tasks.Task RankConsoleSet( string target, string rank )
	{
		try
		{
			var rankUpper = (rank ?? "").Trim().ToUpperInvariant();
			if ( Array.IndexOf( AllowedRanks, rankUpper ) < 0 )
			{
				Log.Warning( "Grade invalide. Autorisés: ADMIN, MODERATOR, VIP, PLAYER" );
				return;
			}

			// Si target est un pseudo connecté -> on récupère sa SteamId, sinon on assume que c'est une SteamId
			var targetConn = FindConnectionByName( target );
			var steamId    = (targetConn != null) ? targetConn.SteamId.ToString() : target;

			var res  = await BackendClient.SetRankAsync( steamId, rankUpper );
			var name = (targetConn != null) ? targetConn.DisplayName : steamId;

			ReceiveSystemMessage( $"[RANK] {name} est maintenant {res.rank}" );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[Console]/cons_rank erreur: {ex.Message}" );
		}
	}


		
	
	
	
	
	
	
	
}