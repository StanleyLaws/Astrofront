using Sandbox;
using System.Linq;
using System.Threading.Tasks;

namespace Astrofront;

public enum MatchState { Lobby = 0, Countdown = 1, InGame = 2, PostGame = 3 }

public sealed class MatchManager : Component, Component.INetworkListener
{
	
	private int _countdownVersion = 0; // permet d’annuler une boucle de décompte en cours
	private bool _countdownLoopActive = false;

	// Helper public pour que d'autres systèmes puissent savoir si une connexion est spectatrice
	public bool IsSpectator( Connection c ) => c != null && _spectators.Contains( c );



	private readonly HashSet<Connection> _spectators = new();



	
    [Property] public int MaxPlayers { get; set; } = 2;      // 1v1 pour les tests
    [Property] public int CountdownStart { get; set; } = 10;  // 5s de décompte

    // Spawns d'équipe (à renseigner dans l'inspecteur)
    [Property] public GameObject[] RedSpawns { get; set; }
    [Property] public GameObject[] BlueSpawns { get; set; }

    [Sync( SyncFlags.FromHost )] public MatchState State { get; private set; } = MatchState.Lobby;
    [Sync( SyncFlags.FromHost )] public int Countdown { get; private set; } = 0;

    // ——— Évènements réseau ———
    public void OnActive( Connection conn )
	{
		if ( !Networking.IsHost ) return;

		// Si la partie est déjà en cours (ou en décompte), les nouveaux sont spectateurs
		if ( State == MatchState.Countdown || State == MatchState.InGame )
		{
			_spectators.Add( conn );
			ChatSystem.ReceiveSystemMessage( $"{conn.DisplayName} a rejoint en spectateur." );
			return; // pas de TryStartCountdown ici
		}

		// Cas Lobby: logique habituelle
		TryStartCountdown();
	}


    public void OnDisconnected( Connection conn )
	{
		if ( !Networking.IsHost ) return;
		
		_spectators.Remove( conn ); // ← nettoyage 

		// Si on perd un joueur pendant le décompte ou la partie
		if ( State == MatchState.Countdown || State == MatchState.InGame )
		{
			if ( Connection.All.Count < MaxPlayers )
			{
				// bump la version -> annule le TryStartCountdown courant
				_countdownVersion++;

				State = MatchState.Lobby;
				Countdown = 0;
				_countdownLoopActive = false; // <— reset le flag
				Log.Info( "Retour Lobby (manque de joueurs)" );
			}
		}
	}


    // ——— Démarrage automatique quand on atteint MaxPlayers ———
    async void TryStartCountdown()
	{
		if ( State != MatchState.Lobby ) return;
		if ( Connection.All.Count != MaxPlayers ) return; // == requis

		// Empêche de lancer plusieurs comptes en parallèle
		if ( _countdownLoopActive ) return;
		_countdownLoopActive = true;


		State = MatchState.Countdown;
		Countdown = CountdownStart; 

		// incrémenter la version et la capturer localement
		var version = ++_countdownVersion;

		Log.Info( $"Début du décompte ({CountdownStart}s)..." );

		while ( Countdown > 0 )
		{
			// Annulation / conditions
			if ( version != _countdownVersion
			  || State != MatchState.Countdown
			  || Connection.All.Count != MaxPlayers )
			{
				ChatSystem.ReceiveSystemMessage( "Décompte annulé (un joueur a quitté ou condition non remplie)." );
				State = MatchState.Lobby;
				Countdown = 0;
				_countdownLoopActive = false;
				return;
			}

			// ➜ Annoncer uniquement à 10s, puis 5..1
			if ( Countdown == 10 || Countdown <= 5 )
			{
				ChatSystem.ReceiveSystemMessage( $"Commencement de la partie dans {Countdown}s" );
			}

			await Task.DelaySeconds( 1f );
			Countdown--;
		}


		// Dernière vérif juste avant le start
		if ( version != _countdownVersion || Connection.All.Count != MaxPlayers )
		{
			ChatSystem.ReceiveSystemMessage( "Décompte annulé." );
			State = MatchState.Lobby;
			Countdown = 0;
			_countdownLoopActive = false;  // <— IMPORTANT
			return;
		}

		ChatSystem.ReceiveSystemMessage( "La partie commence !" );
		_countdownLoopActive = false;      // <— on libère le flag avant de démarrer
		StartMatch();
	}



    // ——— Assignation d'équipes + téléportation sur spawns ———
    void StartMatch()
{
    if ( !Networking.IsHost ) return;

    // 1) Mélange la liste des joueurs pour randomiser QUI va où
    var players = Scene.GetAllComponents<PlayerState>()
    .OrderBy( _ => Game.Random.Float() )
    .ToList();


    int total = players.Count;

    // 2) Objectif d'effectifs par équipe (équilibré)
    int redTarget = total / 2;
    if ( total % 2 == 1 && Game.Random.Int(0,1) == 0 ) // si impair : l'extra va aléatoirement à RED ou BLUE
        redTarget += 1;
    int blueTarget = total - redTarget;

    int redIdx = 0, blueIdx = 0;  // pour itérer les spawns
    int redCount = 0, blueCount = 0;

    // 3) Assigner et téléporter
    for ( int i = 0; i < players.Count; i++ )
    {
        var ps = players[i];

        // Assigne selon les cibles calculées
        var team = (i < redTarget) ? Team.Red : Team.Blue;
        ps.SetTeamHost( team );

        // Choisir un spawn d'équipe
        GameObject spawn = null;
        if ( team == Team.Red && RedSpawns != null && RedSpawns.Length > 0 )
        {
            spawn = RedSpawns[ redIdx % RedSpawns.Length ];
            redIdx++; redCount++;
        }
        else if ( team == Team.Blue && BlueSpawns != null && BlueSpawns.Length > 0 )
        {
            spawn = BlueSpawns[ blueIdx % BlueSpawns.Length ];
            blueIdx++; blueCount++;
        }

        // Téléporter côté propriétaire (client/host) pour que ça prenne partout
        if ( spawn != null )
        {
            ps.TeleportHost( spawn.Transform.World.Position, spawn.Transform.World.Rotation );

            // Log lisible "Pseudo → RED/BLUE"
            var conn = Connection.All.FirstOrDefault( c => c.Id == ps.Network.OwnerId );
            var who  = conn?.DisplayName ?? conn?.Id.ToString() ?? ps.GameObject.Name;
            Log.Info( $"{who} → {team}" );
        }
    }

    State = MatchState.InGame;
    Log.Info( $"Match démarré : {redCount} RED vs {blueCount} BLUE." );
}

}
