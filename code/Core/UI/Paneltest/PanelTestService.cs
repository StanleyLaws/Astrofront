using Sandbox;
using Astrofront;
using Sandbox;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Astrofront;

/// <summary>
/// Service tout-en-un pour le panel test:
/// - CLIENT: écoute l'input "view" (toggle), "menu" (fermer).
/// - LISTEN-SERVER (host local): scan direct côté client, sans réseau.
/// - MULTI/DÉDIÉ: envoie un RPC host avec requesterId; le host scanne
///   autour du PlayerState du demandeur et renvoie au bon client.
/// - UI: PanelTestUI.Show(items) (aucune création de GO UI UI ici).
/// </summary>
public sealed class PanelTestService : Component
{
    [Property] public float ScanRadius { get; set; } = 160f;

    /// <summary>Si true, on lit l'input local (mettre UNE fois dans la scène côté client).</summary>
    [Property] public bool ListenInput { get; set; } = true;

    /// <summary>Option dev: si l'UI n'existe pas en local (listen-server), on la crée une fois.</summary>
    [Property] public bool AutoCreateUiIfMissing { get; set; } = true;


    protected override void OnUpdate()
	{
		// Ce service continue d'exister pour les RPC / scan,
		// mais on NE lit plus l'input "view" ici.
		if ( IsProxy ) return;

		// Si un jour tu veux réactiver "view" en debug, tu
		// pourras remettre ton ancien code ici.
	}


	
	// ======================================================================
//  API statique appelée par UiInteractionController / UiInventoryCoordinator
// ======================================================================

/// <summary>
/// Vérifie s'il y a des ResourcePickup à portée du joueur local.
/// </summary>
public static bool HasLootNearbyForLocal()
{
    var scene = Game.ActiveScene;
    if ( scene == null ) return false;

    // On récupère n'importe quel PanelTestService non proxy pour connaître le rayon
    var svc = scene.GetAllComponents<PanelTestService>()?.FirstOrDefault( c => c != null && !c.IsProxy );
    if ( svc == null ) return false;

    // On réutilise le scan déjà en place
    var items = ScanPickupsAroundLocal( svc.ScanRadius );
    return items != null && items.Length > 0;
}

/// <summary>
/// Ouvre le panel de loot pour le joueur local (sans toggle).
/// </summary>
public static void OpenLootForLocal()
{
    var scene = Game.ActiveScene;
    if ( scene == null ) return;

    // Si déjà ouvert, on ne fait rien
    if ( PanelTestUI.Instance?.IsOpen == true )
        return;

    // Host local (listen-server) : on scanne et on affiche direct
    if ( Networking.IsHost )
    {
        var svc = scene.GetAllComponents<PanelTestService>()?.FirstOrDefault( c => c != null && !c.IsProxy );
        if ( svc == null ) return;

        var items = ScanPickupsAroundLocal( svc.ScanRadius );
        PanelTestUI.Show( items );
    }
    else
    {
        // Client pur : demande au host via RPC
        var id = Connection.Local?.Id.ToString();
        if ( !string.IsNullOrEmpty( id ) )
        {
            RequestOpenForLocalHost( id );
        }
    }
}

/// <summary>
/// Version toggle : si déjà ouvert -> ferme, sinon ouvre.
/// Utilisée pour le input "view".
/// </summary>
public static void RequestOpenLootForLocal()
{
    // Si le panel est déjà ouvert, on le ferme
    if ( PanelTestUI.Instance?.IsOpen == true )
    {
        PanelTestUI.Hide();
        return;
    }

    // Sinon on l’ouvre
    OpenLootForLocal();
}

/// <summary>
/// Rafraîchit le panel de loot pour le joueur local,
/// si celui-ci est ouvert.
/// </summary>
public static void RefreshLootForLocal()
{
    var scene = Game.ActiveScene;
    if ( scene == null ) return;

    // Si le panel n'est pas ouvert côté client, on ne fait rien
    if ( PanelTestUI.Instance == null || !PanelTestUI.Instance.IsOpen )
        return;

    // === HOST (listen-server / dédie qui a aussi un client local) ===
    if ( Networking.IsHost )
    {
        // On récupère un PanelTestService non proxy pour connaître le rayon
        var svc = scene.GetAllComponents<PanelTestService>()
                       ?.FirstOrDefault( c => c != null && !c.IsProxy );
        if ( svc == null ) return;

        // On rescanne autour du joueur local
        var items = ScanPickupsAroundLocal( svc.ScanRadius );

        // On rebâtit juste l'UI locale
        PanelTestUI.Show( items );
    }
    else
    {
        // === CLIENT pur : on redemande au host via le même RPC que pour l'ouverture ===
        var id = Connection.Local?.Id.ToString();
        if ( !string.IsNullOrEmpty( id ) )
        {
            RequestOpenForLocalHost( id );
        }
    }
}


	
	
	
	
	
	

    // ———————————————————————————————————————————————————————————————————
    // Listen-server : scan des ResourcePickup autour du joueur local.
    // ———————————————————————————————————————————————————————————————————
    private static PanelItemDto[] ScanPickupsAroundLocal( float radius )
    {
        var scene = Game.ActiveScene;
        if ( scene == null ) return Array.Empty<PanelItemDto>();

        // 1) Origine = PlayerState du client local si dispo
        var me = scene.GetAllComponents<PlayerState>()
                      ?.FirstOrDefault(p => p?.Network?.Owner != null
                                          && Connection.Local != null
                                          && p.Network.Owner.Id == Connection.Local.Id);

        var origin = me != null
            ? me.GameObject.Transform.World.Position
            : (scene.Camera?.Transform.World.Position ?? default);

        var r2 = radius * radius;

        var pickups = scene.GetAllComponents<ResourcePickup>()
            .Where(p => p != null && p.Amount > 0
                     && (p.Transform.World.Position - origin).LengthSquared <= r2)
            .OrderBy(p => (p.Transform.World.Position - origin).LengthSquared)
            .Select(p => new PanelItemDto
            {
                Id = p.GameObject.Id,
                Type = p.Type.ToString(),
                Amount = p.Amount
            })
            .ToArray();

        return pickups;
    }

    // ———————————————————————————————————————————————————————————————————
    // Client → Host : demande d’ouverture pour "requesterId"
    // ———————————————————————————————————————————————————————————————————
    [Rpc.Host]
    public static void RequestOpenForLocalHost( string requesterId )
    {
        if ( string.IsNullOrEmpty( requesterId ) ) return;

        var scene = Game.ActiveScene;
        var svc = scene?.GetAllComponents<PanelTestService>()?.FirstOrDefault();
        if ( svc == null )
        {
            var go = scene?.CreateObject();
            go.Name = "PanelTestService";
            svc = go.Components.Create<PanelTestService>();
            Log.Info("[PanelTest] Service host créé par RPC.");
        }

        svc.OpenFor( requesterId );
    }

    // ———————————————————————————————————————————————————————————————————
    // HOST : construit la liste d’items autour du PlayerState du client demandeur
    // et renvoie uniquement à ce client
    // ———————————————————————————————————————————————————————————————————
    public void OpenFor( string requesterId )
    {
        if ( !Networking.IsHost || string.IsNullOrEmpty( requesterId ) ) return;

        var ps = Scene?.GetAllComponents<PlayerState>()
                      ?.FirstOrDefault( p => p?.Network?.Owner != null
                                           && p.Network.Owner.Id.ToString() == requesterId );

        var origin = ps != null
            ? ps.GameObject.Transform.World.Position
            : (Scene?.Camera?.Transform.World.Position ?? default);

        float r2 = ScanRadius * ScanRadius;

        var items = Scene.GetAllComponents<ResourcePickup>()
            .Where( p => p != null && p.Amount > 0
                      && (p.Transform.World.Position - origin).LengthSquared <= r2 )
            .OrderBy( p => (p.Transform.World.Position - origin).LengthSquared )
            .Select( p => new PanelItemDto { Id = p.GameObject.Id, Type = p.Type.ToString(), Amount = p.Amount } )
            .ToArray();

        ShowPanelClient( requesterId, items );
    }

    // ———————————————————————————————————————————————————————————————————
    // CLIENT : ouverture UI pour la cible (filtré par Connection.Local.Id)
    // ———————————————————————————————————————————————————————————————————
    [Rpc.Broadcast]
    private void ShowPanelClient( string targetId, PanelItemDto[] items )
    {
        if ( Connection.Local == null || Connection.Local.Id.ToString() != targetId )
            return;

        PanelTestUI.Show( items );
    }

    // ———————————————————————————————————————————————————————————————————
    // Dev helper : crée un ScreenPanel + PanelTestUI si manquants (listen-server)
    // ———————————————————————————————————————————————————————————————————
    private void EnsureLocalUiExistsIfNeeded()
    {
        if ( PanelTestUI.Instance != null || !AutoCreateUiIfMissing ) return;

        var scene = Game.ActiveScene;
        if ( scene == null ) return;

        var go = scene.CreateObject();
        go.Name = "PanelTestUI_Auto";
        go.Components.Create<ScreenPanel>();
        go.Components.Create<PanelTestUI>();
    }

    // ———————————————————————————————————————————————————————————————————
    // DTO
    // ———————————————————————————————————————————————————————————————————
    [Serializable]
    public struct PanelItemDto
    {
        public Guid Id;
        public string Type;
        public int Amount;
    }
}
