using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Astrofront
{
    // ---------- Config par ressource ----------
    [Serializable]
    public class ResourceLine
    {
        [Property] public ResourceType Type { get; set; } = ResourceType.Stellium;
        [Property] public float TickSeconds { get; set; } = 5f;   // ex: +1 toutes les 5s
        [Property] public int   AmountPerTick { get; set; } = 1;  // montant par tick
        [Property] public int   MaxStoredTicks { get; set; } = 30; // cap d’accumulation par joueur
    }

    /// <summary>
    /// Dispenser d’équipe (1 par station).
    /// Accumulation INDIVIDUELLE par joueur (indexée par SteamId ou Connection.Id selon le flag).
    /// </summary>
    public sealed class StationResourceGenerator : Component, Component.INetworkListener
    {
        [Property, Title("Owning Team")]
        public Team Team { get; set; } = Team.Red;

        [Property, Title("Resources")]
        public ResourceLine[] Resources { get; set; } =
        {
            new ResourceLine{ Type = ResourceType.Stellium, TickSeconds = 5f,  AmountPerTick = 1, MaxStoredTicks = 30 },
            new ResourceLine{ Type = ResourceType.Plasma,   TickSeconds = 12f, AmountPerTick = 1, MaxStoredTicks = 20 },
            new ResourceLine{ Type = ResourceType.Alloy,    TickSeconds = 20f, AmountPerTick = 1, MaxStoredTicks = 15 }
        };

        // Permet de distinguer 2 instances locales qui partagent le même SteamID (utile en éditeur).
        [Property, Title("Use Connection.Id for local tests")]
        public bool UseConnectionKeyForTests { get; set; } = true;

        // Instance → utilise le flag de CET objet
        private string PlayerKey( Connection c ) => PlayerKeyFor( c, UseConnectionKeyForTests );

        // Statique → utilisable depuis les méthodes static (comme inv_report)
        private static string PlayerKeyFor( Connection c, bool useConnId )
            => useConnId ? c?.Id.ToString() : c?.SteamId.ToString();

        // Pour chaque ressource : key (SteamId ou ConnId) -> lastClaimEpochSeconds
        private readonly Dictionary<ResourceType, Dictionary<string, double>> _lastClaim = new();

        protected override void OnStart()
        {
            if ( Resources == null || Resources.Length == 0 )
            {
                Resources = new[]
                {
                    new ResourceLine{ Type = ResourceType.Stellium, TickSeconds = 5f,  AmountPerTick = 1, MaxStoredTicks = 30 },
                    new ResourceLine{ Type = ResourceType.Plasma,   TickSeconds = 12f, AmountPerTick = 1, MaxStoredTicks = 20 },
                    new ResourceLine{ Type = ResourceType.Alloy,    TickSeconds = 20f, AmountPerTick = 1, MaxStoredTicks = 15 }
                };
            }

            foreach ( var r in Resources )
                _lastClaim[r.Type] = new Dictionary<string, double>();
        }

        // Quand une connexion devient active, on démarre son horloge à "maintenant"
        public void OnActive( Connection connection )
        {
            if ( !Networking.IsHost || connection is null ) return;

            var sid = PlayerKey( connection );
            var now = Time.Now;

            foreach ( var r in Resources )
            {
                var map = _lastClaim[r.Type];
                if ( !map.ContainsKey( sid ) )
                    map[sid] = now; // commence l’accumulation maintenant
            }
        }

        public void OnDisconnected( Connection connection )
        {
            // Rien à faire : on garde l’horodatage si la connexion revient
        }

        // ---- Calcul du disponible pour (clé, ressource) à l’instant 'now' ----
        private int GetAvailableAmount( string playerKey, ResourceLine line, double now )
        {
            if ( !_lastClaim.TryGetValue( line.Type, out var map ) )
                return 0;

            if ( !map.TryGetValue( playerKey, out var last ) )
                last = now; // inconnu => rien d'accumulé

            var elapsed = Math.Max( 0.0, now - last );
            var period  = Math.Max( 0.0001f, line.TickSeconds );
            var ticks   = (int)Math.Floor( elapsed / period );

            if ( line.MaxStoredTicks > 0 )
                ticks = Math.Min( ticks, line.MaxStoredTicks );

            if ( ticks <= 0 ) return 0;

            return ticks * Math.Max( 1, line.AmountPerTick );
        }

        // ---- Réclamer (serveur) une ressource pour une connexion ----
        // ---- Réclamer (serveur) une ressource pour une connexion ----
		public void ServerClaim( Connection conn, ResourceType t )
		{
			if ( !Networking.IsHost || conn is null ) return;

			var line = Resources?.FirstOrDefault( x => x.Type == t );
			if ( line == null ) return;

			var map = _lastClaim[t];
			var sid = PlayerKey( conn );
			var now = Time.Now;

			// Si la clé n'existe pas encore, initialise et ne crédite rien cette frame
			if ( !map.ContainsKey( sid ) )
			{
				map[sid] = now;
				return;
			}

			var amount = GetAvailableAmount( sid, line, now );
			if ( amount <= 0 ) return;

			var inv = FindInventoryFor( conn );
			if ( inv == null )
			{
				Log.Info( $"[CLAIM] Inventaire introuvable pour ConnId={conn.Id}" );
				return;
			}

			// === Ajout capacity-aware, puis synchro client ===
			// AddResourceStacks retourne le "reste" non ajouté.
			int remain   = inv.AddResourceStacks( t, amount );
			int accepted = amount - remain;

			if ( accepted <= 0 )
			{
				// inventaire plein -> ne rien débiter de l'accumulation
				Log.Info( $"[CLAIM] {conn.DisplayName} inventaire plein, 0/{amount} {t} pris." );
				return;
			}

			// Push côté client owner (garde ton protocole existant)
			inv.AddResourceClient( conn.Id.ToString(), t, accepted );

			// === Ne débiter l'accumulation qu'à hauteur du pris ===
			// On exprime en "ticks" pour rester cohérent avec GetAvailableAmount()
			var period        = Math.Max( 0.0001f, line.TickSeconds );
			var perTick       = Math.Max( 1, line.AmountPerTick );
			var ticksAvail    = amount   / perTick;
			var ticksAccepted = accepted / perTick;

			// Laisser le reliquat accumulé : reculer "last" du nombre de ticks non consommés
			var remainingTicks = Math.Max( 0, ticksAvail - ticksAccepted );
			map[sid] = now - remainingTicks * period;

			Log.Info( $"[CLAIM] {conn.DisplayName} +{accepted}/{amount} {t} (ticks pris {ticksAccepted}/{ticksAvail})" );
		}



        // ---- RPC Host: réclame TOUTES les ressources pour l’appelant ----
        [Rpc.Host]
        public void ClaimAllHost()
        {
            var caller = Rpc.Caller ?? Connection.Local;
            if ( caller is null ) return;

            foreach ( var line in Resources )
                ServerClaim( caller, line.Type );
        }

        // ---- Commandes de test ----

        // 1) Réclamer tout (utile pour tester sans interaction)
        [ConCmd( "claim_all" )]
        public static void CmdClaimAll()
        {
            var scene = Game.ActiveScene;
            var gen = scene?.GetAllComponents<StationResourceGenerator>()?.FirstOrDefault();
            if ( gen == null ) return;

            gen.ClaimAllHost(); // RPC Host vers le serveur
        }

        // 2) Rapport des ressources (disponibles + inventaire) pour chaque joueur actif
        [ConCmd( "inv_report" )]
        public static void CmdInventoryReport()
        {
            var scene = Game.ActiveScene;
            if ( scene == null )
            {
                Log.Info( "[INV] Pas de scène active." );
                return;
            }

            var gens = scene.GetAllComponents<StationResourceGenerator>()?.ToList()
                      ?? new List<StationResourceGenerator>();
            var gen = gens.FirstOrDefault();
            if ( gen == null )
            {
                Log.Info( "[INV] Aucun StationResourceGenerator trouvé." );
                return;
            }

            bool useConnKey = gen.UseConnectionKeyForTests;

            var allInv = scene.GetAllComponents<InventorySystem>()?.ToList() ?? new List<InventorySystem>();

            Log.Info( "=== INVENTORY REPORT ===" );
            foreach ( var c in Connection.All.Where( cc => cc != null && cc.IsActive ) )
            {
                // Inventaire totalisé par type (à partir des slots)
                int stInv = 0, plInv = 0, alInv = 0;
                {
                    var inv = gen.FindInventoryFor( c );
                    if ( inv != null )
                    {
                        var snap = inv.GetAllResourcesSnapshot();
                        snap.TryGetValue( ResourceType.Stellium, out stInv );
                        snap.TryGetValue( ResourceType.Plasma,   out plInv );
                        snap.TryGetValue( ResourceType.Alloy,    out alInv );
                    }
                }

                // Disponibles (accumulées mais non réclamées)
                var sid = PlayerKeyFor( c, useConnKey );
                var now = Time.Now;
                int stAvail = 0, plAvail = 0, alAvail = 0;

                foreach ( var line in gen.Resources )
                {
                    var amt = gen.GetAvailableAmount( sid, line, now );
                    if ( amt <= 0 ) continue;

                    switch ( line.Type )
                    {
                        case ResourceType.Stellium: stAvail = amt; break;
                        case ResourceType.Plasma:   plAvail = amt; break;
                        case ResourceType.Alloy:    alAvail = amt; break;
                    }
                }

                Log.Info( $" - {c.DisplayName} (ConnId={c.Id}, Steam={c.SteamId})" );
                Log.Info( $"     Dispo:      Stellium={stAvail} | Plasma={plAvail} | Alloy={alAvail}" );
                Log.Info( $"     Inventaire: Stellium={stInv} | Plasma={plInv} | Alloy={alInv}" );
            }
            Log.Info( "========================" );
        }

        // ---- Utilitaires ----

        // Trouver l'inventaire appartenant à 'conn' (pas de fallback hasardeux)
        private InventorySystem FindInventoryFor( Connection conn )
		{
			if ( conn == null ) return null;

			// 1) Direct: un InventorySystem dont l'Owner réseau est la connexion
			var inv = Scene?.GetAllComponents<InventorySystem>()
							?.FirstOrDefault(i => i != null
											   && i.Network != null
											   && i.Network.Owner == conn);
			if ( inv != null ) return inv;

			// 2) Via le PlayerState de cette connexion
			var ps = Scene?.GetAllComponents<PlayerState>()
						   ?.FirstOrDefault(p => p != null
											  && p.Network != null
											  && p.Network.Owner == conn);
			if ( ps == null ) return null;

			// 3) Sur le GO du joueur (ou enfant)
			inv = ps.GameObject?.Components.Get<InventorySystem>( FindMode.InSelf | FindMode.InChildren );
			if ( inv != null ) return inv;

			// 4) Dernier recours: on le crée côté serveur (pour que la claim ne rate jamais)
			if ( Networking.IsHost && ps.GameObject != null )
			{
				inv = ps.GameObject.Components.Create<InventorySystem>();
				return inv;
			}

			return null;
		}

    }
}
