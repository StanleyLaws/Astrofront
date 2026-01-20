using Sandbox;
using System.Linq;

namespace Astrofront;

/// <summary>
/// Pont “UI ⇄ Monde” :
/// - INVENTAIRE → MONDE : RequestSpawnToWorld (drop depuis l’inventaire)
/// - LOOT → INVENTAIRE : RequestLootToInventory (ramassage via PanelTestUI + inventaire)
/// - (optionnel) RequestConsumeFromWorld reste pour d’autres usages simples.
/// </summary>
public sealed class UiLootInventoryBridge : Component
{
    public static UiLootInventoryBridge Instance { get; private set; }

    /// <summary>Rayon de recherche des pickups autour du joueur.</summary>
    [Property] public float LootRadius { get; set; } = 160f;

    protected override void OnStart()
    {
        if ( Instance == null )
        {
            Instance = this;
        }
        else if ( Instance != this )
        {
            Log.Warning( "[UiLootInventoryBridge] Il existe déjà une instance, celle-ci sera ignorée." );
        }
    }

    protected override void OnDestroy()
    {
        if ( Instance == this )
            Instance = null;
    }

    // ==========================================================
    //  MONDE → INVENTAIRE : chemin “simple” (consommer des pickups)
    //  (On peut encore s’en servir ailleurs si besoin)
    // ==========================================================

    public static void RequestConsumeFromWorld( ResourceType type, int amount )
    {
        if ( amount <= 0 ) return;

        if ( Instance == null )
        {
            Log.Warning( "[UiLootInventoryBridge] Pas d'instance dans la scène – rien ne sera retiré du monde." );
            return;
        }

        Instance.ConsumeFromWorldHost( type, amount, Instance.LootRadius );
    }

    [Rpc.Host]
    private void ConsumeFromWorldHost( ResourceType type, int requested, float radius )
    {
        var caller = Rpc.Caller ?? Connection.Local;
        if ( caller == null || requested <= 0 ) return;

        var ps = FindPlayerState( caller );
        if ( ps == null ) return;

        var pos = ps.GameObject.Transform.World.Position;
        float r2 = radius * radius;

        int remaining    = requested;
        int removedTotal = 0;

        var pickups = Scene.GetAllComponents<ResourcePickup>()
            .Where( p => p != null
                      && p.Amount > 0
                      && p.Type == type
                      && (p.Transform.World.Position - pos).LengthSquared <= r2 )
            .OrderBy( p => (p.Transform.World.Position - pos).LengthSquared )
            .ToList();

        foreach ( var p in pickups )
        {
            if ( remaining <= 0 )
                break;

            int take = System.Math.Min( remaining, p.Amount );
            p.Amount -= take;
            remaining -= take;
            removedTotal += take;

            if ( p.Amount <= 0 )
            {
                p.Amount = 0;
                p.GameObject.Destroy();
            }
        }

        Log.Info( $"[UiLootInventoryBridge] Consume type={type}, requested={requested}, removed={removedTotal}" );
    }

    /// <summary>
    /// Alias ancien nom (au cas où il reste des appels). Le paramètre radius est ignoré.
    /// </summary>
    public static void RequestMoveFromWorldToInventory( ResourceType type, int amount, float radius )
    {
        RequestConsumeFromWorld( type, amount );
    }

    // ==========================================================
    //  LOOT → INVENTAIRE : chemin sécurisé, anti-duplication
    // ==========================================================

    /// <summary>
    /// Appelé côté client quand on dépose dans l’inventaire un stack
    /// qui PROVIENT du loot (PanelTestUI).
    /// 
    /// On NE modifie PAS l’inventaire local ici : on demande au host
    /// de faire la transaction en respectant :
    ///  - ce qu’il y a VRAIMENT au sol
    ///  - la capacité de l’inventaire
    /// </summary>
    public static void RequestLootToInventory( ResourceType type, int requested, int slotIndex )
    {
        if ( requested <= 0 ) return;

        if ( Instance == null )
        {
            Log.Warning( "[UiLootInventoryBridge] Pas d'instance – RequestLootToInventory ignoré." );
            return;
        }

        Instance.LootToInventoryHost( type, requested, slotIndex, Instance.LootRadius );
    }

    [Rpc.Host]
    private void LootToInventoryHost( ResourceType type, int requested, int slotIndex, float radius )
    {
        var caller = Rpc.Caller ?? Connection.Local;
        if ( caller == null || requested <= 0 ) return;

        var inv = FindInventoryFor( caller );
        var ps  = FindPlayerState( caller );
        if ( inv == null || ps == null ) return;

        var pos = ps.GameObject.Transform.World.Position;
        float r2 = radius * radius;

        // 1) Combien il y a VRAIMENT au sol de ce type ?
        var pickups = Scene.GetAllComponents<ResourcePickup>()
            .Where( p => p != null
                      && p.Amount > 0
                      && p.Type == type
                      && (p.Transform.World.Position - pos).LengthSquared <= r2 )
            .OrderBy( p => (p.Transform.World.Position - pos).LengthSquared )
            .ToList();

        int available = 0;
        foreach ( var p in pickups )
            available += p.Amount;

        if ( available <= 0 )
        {
            Log.Info( $"[UiLootInventoryBridge] LootToInventoryHost : aucun {type} à portée." );
            LootToInventoryResultClient( caller.Id.ToString(), type, requested, 0 );
            return;
        }

        int maxByWorld = System.Math.Min( requested, available );
        if ( maxByWorld <= 0 )
        {
            LootToInventoryResultClient( caller.Id.ToString(), type, requested, 0 );
            return;
        }

        // 2) Ajouter dans l’inventaire serveur en respectant la capacité globale
        int added = inv.AddToSlot( slotIndex, type, maxByWorld );
        if ( added <= 0 )
        {
            Log.Info( "[UiLootInventoryBridge] LootToInventoryHost : inventaire plein, rien ajouté." );
            LootToInventoryResultClient( caller.Id.ToString(), type, requested, 0 );
            return;
        }

        // Synchroniser le client propriétaire de l’inventaire
        inv.AddToSlotOwner( slotIndex, type, added );

        // 3) Retirer 'added' unités des pickups au sol
        int remaining = added;

        foreach ( var p in pickups )
        {
            if ( remaining <= 0 )
                break;

            int take = System.Math.Min( remaining, p.Amount );
            p.Amount -= take;
            remaining -= take;

            if ( p.Amount <= 0 )
            {
                p.Amount = 0;
                p.GameObject.Destroy();
            }
        }

        Log.Info( $"[UiLootInventoryBridge] LootToInventoryHost type={type}, requested={requested}, added={added}" );

        // 4) Informer UNIQUEMENT le client concerné
        LootToInventoryResultClient( caller.Id.ToString(), type, requested, added );
    }

    [Rpc.Broadcast]
    private void LootToInventoryResultClient( string targetId, ResourceType type, int requested, int accepted )
    {
        var local = Connection.Local;
        if ( local == null || local.Id.ToString() != targetId )
            return;

        Log.Info( $"[UiLootInventoryBridge] Client result type={type}, requested={requested}, accepted={accepted}" );

        // On retire seulement ce que le host a VRAIMENT accepté
        if ( accepted > 0 )
        {
            UiDragContext.TakeFromHand( accepted );
        }

        // L’inventaire a été modifié côté host → le mirror local est mis à jour
        // via AddToSlotOwner. On rafraîchit juste l’UI si elle est ouverte.
        InventoryManagePanel.Instance?.BuildSlotsFromInventory();
    }

    // ==========================================================
    //  INVENTAIRE → MONDE : spawn de pickups
    // ==========================================================

    public static void RequestSpawnToWorld( ResourceType type, int amount )
    {
        if ( amount <= 0 ) return;

        if ( Instance == null )
        {
            Log.Warning( "[UiLootInventoryBridge] Pas d'instance – aucun pickup ne sera spawn." );
            return;
        }

        Instance.SpawnToWorldHost( type, amount );
    }

    [Rpc.Host]
private void SpawnToWorldHost( ResourceType type, int amount )
{
    var caller = Rpc.Caller ?? Connection.Local;
    if ( caller == null || amount <= 0 ) return;

    var ps = Scene?.GetAllComponents<PlayerState>()
                   ?.FirstOrDefault( p => p != null
                                       && p.Network != null
                                       && p.Network.Owner == caller );
    if ( ps == null ) return;

    var trPlayer = ps.GameObject.Transform.World;

    // Une petite boucle pour spawn 'amount' pickups d'1 unité chacun.
    for ( int i = 0; i < amount; i++ )
    {
        var spawnPos = trPlayer.Position
                      + trPlayer.Rotation.Forward * 48f
                      + Vector3.Up * 8f
                      + Vector3.Random.Normal * 4f; // léger scatter

        var go = Scene.CreateObject();
        go.Name = $"pickup_{type}";
        go.Transform.World = new Transform( spawnPos, Rotation.Identity );

        var p = go.Components.Create<ResourcePickup>();
        p.Type   = type;
        p.Amount = 1;

        go.NetworkSpawn();
    }

    Log.Info( $"[UiLootInventoryBridge] Spawned {amount} pickups type={type}, amount=1 each" );
}


    // ==========================================================
    //  Helpers serveur
    // ==========================================================

    private InventorySystem FindInventoryFor( Connection conn )
    {
        var ps = FindPlayerState( conn );
        if ( ps == null ) return null;

        return ps.GameObject?.Components.Get<InventorySystem>( FindMode.InSelf | FindMode.InChildren );
    }

    private PlayerState FindPlayerState( Connection conn )
    {
        return Scene?.GetAllComponents<PlayerState>()
                    ?.FirstOrDefault( p => p != null
                                        && p.Network != null
                                        && p.Network.Owner == conn );
    }
}
