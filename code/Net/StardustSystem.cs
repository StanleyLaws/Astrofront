using Sandbox;
using System;
using System.Collections.Generic;

namespace Astrofront; 

/// Attribue des points "Stardust" en fonction du temps connecté (autorité serveur)
public sealed class StardustSystem : Component, Component.INetworkListener
{
    // Réglages de base
    [Property] public int PointsPerTick { get; set; } = 1;      // +1 Stardust...
    [Property] public float SecondsPerTick { get; set; } = 60;  // ...chaque 60s de présence
    [Property] public bool AnnounceInChat { get; set; } = true; // annonce via chat

    // État par connexion 
    private readonly Dictionary<Guid, TimeSince> _sinceLastAward = new(); // Connection.Id -> timer 

    protected override void OnUpdate()
    {
        if ( !Networking.IsHost ) return; // logique serveur uniquement

        foreach ( var conn in Connection.All )
        {
            // skip si pas pleinement active
            if ( conn == null || !conn.IsActive )
                continue;

            var id = conn.Id; // Guid unique de la connexion

            if ( !_sinceLastAward.TryGetValue( id, out var t ) )
            {
                _sinceLastAward[id] = 0;
                continue;
            }

            if ( t > SecondsPerTick )
            {
                _sinceLastAward[id] = 0;
                AwardLocal( conn, PointsPerTick );
            }
        }
    }

    // Donne des points localement + envoie au backend
    private void AwardLocal( Connection conn, int amount )
    {
        // Log local
        Log.Info( $"[STARDUST] +{amount} pour {conn.DisplayName}" );

        if ( AnnounceInChat )
        {
            ChatSystem.ReceiveSystemMessage( $"{conn.DisplayName} a gagné +{amount} Stardust" );
        }

        // Envoi non bloquant au backend (pas de Task.Run -> compatible whitelist)
        SendToBackend( conn, amount );
    }

    private async void SendToBackend( Connection conn, int amount )
    {
        try
        {
            var idKey = Guid.NewGuid().ToString(); // clé d'idempotence unique
            var resp = await BackendClient.AddCurrencyAsync(
                conn.SteamId.ToString(),
                "STARDUST",
                amount,
                "tick_reward",
                "Lobby_01", // ⚠️ remplace par AF_Game_01 sur ce serveur-là
                idKey
            );

            Log.Info( $"[Bank] +{resp.delta} {resp.currency} -> nouveau solde: {resp.newAmount}" );
        }
        catch ( Exception ex )
        {
            Log.Warning( $"[Bank] Erreur envoi Stardust: {ex.Message}" );
        }
    }

    // Hooks réseau
    public void OnActive( Connection connection )
	{
		_sinceLastAward[connection.Id] = 0;

		// ➜ Récupère et affiche le solde initial
		FetchAndAnnounceBalance( connection );
		FetchAndAnnounceMoney( connection );

		
		GiveWelcomeMoney( connection );
	}


    public void OnDisconnected( Connection connection )
    {
        _sinceLastAward.Remove( connection.Id );
    }
	
	private async void FetchAndAnnounceBalance( Connection conn )
	{
		try
		{
			var bal = await BackendClient.GetBalanceAsync( conn.SteamId.ToString(), "STARDUST" );

			// Log serveur + message chat (optionnel)
			Log.Info( $"[Bank] Solde initial pour {conn.DisplayName}: {bal.amount} STARDUST" );
			if ( AnnounceInChat )
			{
				ChatSystem.ReceiveSystemMessage( $"{conn.DisplayName} a {bal.amount} Stardust" );
			}
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[Bank] Erreur lecture balance: {ex.Message}" );
		}
	}
	
	
	private async void GiveWelcomeMoney( Connection conn )
	{
		try
		{
			var idKey = Guid.NewGuid().ToString();
			var resp = await BackendClient.AddCurrencyAsync(
				conn.SteamId.ToString(),
				"MONEY",       // <- nouvelle devise
				10,            // <- +10 pour test
				"welcome_bonus",
				"Lobby_01",
				idKey
			);

			Log.Info($"[Bank] +{resp.delta} {resp.currency} -> nouveau solde: {resp.newAmount}");
			if ( AnnounceInChat )
				ChatSystem.ReceiveSystemMessage( $"{conn.DisplayName} a reçu +10 MONEY (total {resp.newAmount})" );
		}
		catch ( Exception ex )
		{
			Log.Warning($"[Bank] Erreur bonus MONEY: {ex.Message}");
		}
	}


	private async void FetchAndAnnounceMoney( Connection conn )
	{
		try
		{
			var bal = await BackendClient.GetBalanceAsync( conn.SteamId.ToString(), "MONEY" );
			Log.Info( $"[Bank] Solde MONEY pour {conn.DisplayName}: {bal.amount}" );
			if ( AnnounceInChat )
			{
				ChatSystem.ReceiveSystemMessage( $"{conn.DisplayName} a {bal.amount} Money" );
			}
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[Bank] Erreur lecture balance MONEY: {ex.Message}" );
		}
	}

	
	
	
	
	
}
