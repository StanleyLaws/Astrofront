using Sandbox;
using System;
using System.Collections.Generic;

namespace Astrofront;   

public sealed class InventorySystem : Component
{
    // ====== Modèle : 1 slot par type de ressource ======
    [Serializable]
    public struct Slot
    {
        public bool HasItem;
        public ResourceType Type;
        public int Count;
    }

    // 0: Stellium, 1: Plasma, 2: Alloy — tu peux laisser 5 si ton HUD l’attend, les 2 derniers resteront vides
    [Property] public int SlotCount { get; set; } = 5;

    [Property] public int SelectedIndex { get; private set; } = 0;

    // (obsolète pour la logique de capacité, on la garde juste si ton HUD s’y réfère)
    [Property] public int ResourceMaxStack { get; set; } = 999999;

    // ==== NOUVEAU : capacité globale ====
    [Property, Title("Inventory Capacity (space units)")]
    public int CapacitySpace { get; set; } = 10;

    // Coût “place” par ressource
    private static int UnitSpace(ResourceType t) => t switch
    {
        ResourceType.Stellium => 2, // ← demandé
        ResourceType.Plasma   => 4,
        ResourceType.Alloy    => 6,
        _ => 1
    };

    private Slot[] _slots;

    // UI events
    public event Action<int> SelectionChanged;
    public event Action SlotsChanged;

    protected override void OnStart()
    {
        if ( SlotCount < 3 ) SlotCount = 3;          // on veut au moins 3 pour nos 3 ressources
        if ( _slots == null || _slots.Length != SlotCount )
            _slots = new Slot[SlotCount];

        // Option : “verrouiller” l’index/type des 3 premiers slots
        InitFixedResourceSlots();

        SelectedIndex = SelectedIndex.Clamp( 0, SlotCount - 1 );
        SelectionChanged?.Invoke( SelectedIndex );
    }

    private void InitFixedResourceSlots()
{
    // Slot 0 = HANDS réservé
    ref var s = ref _slots[0];
    s.HasItem = false;
    s.Count   = 0;
    s.Type    = default; // ou un type spécial si tu en ajoutes
}


    private void EnsureSlotType(int index, ResourceType t)
    {
        ref var s = ref _slots[index];
        s.Type = t; // on garde le type même si HasItem=false
        if ( s.Count <= 0 ) s.HasItem = false;
    }

    protected override void OnUpdate()
    {
        if ( IsProxy ) return;
		
				// Bloque toute interaction d'inventaire quand une UI modale est ouverte
		if ( UiModalController.IsUiLockedLocal )
		{
			return; // plus de sélection ni drop
		}

		
		

        // entrées existantes
        if ( Input.Pressed( "slot1" ) ) SetSelected( 0 );
        if ( Input.Pressed( "slot2" ) && SlotCount >= 2 ) SetSelected( 1 );
        if ( Input.Pressed( "slot3" ) && SlotCount >= 3 ) SetSelected( 2 );
        if ( Input.Pressed( "slot4" ) && SlotCount >= 4 ) SetSelected( 3 );
        if ( Input.Pressed( "slot5" ) && SlotCount >= 5 ) SetSelected( 4 );

        if ( Input.Pressed( "drop" ) ) DropOneHost( SelectedIndex );

        var wheel = Input.MouseWheel;
        if ( wheel.y > 0f ) Prev();
        else if ( wheel.y < 0f ) Next();
    }

    public void Next() => SetSelected( (SelectedIndex + 1) % SlotCount );

    public void Prev()
    {
        var prev = SelectedIndex - 1;
        if ( prev < 0 ) prev = SlotCount - 1;
        SetSelected( prev );
    }

    public void SetSelected( int index )
    {
        index = index.Clamp( 0, SlotCount - 1 );
        if ( index == SelectedIndex ) return;
        SelectedIndex = index;
        SelectionChanged?.Invoke( SelectedIndex );
    }

    // ====== CAPACITÉ ======
    public int UsedSpace()
    {
        int used = 0;
        for ( int i = 1; i < _slots.Length; i++ ) // <-- on saute le slot 0 (Hands)
        {
            ref var s = ref _slots[i];
            if ( s.Count > 0 )
                used += s.Count * UnitSpace( s.Type );
        }
        return used;
    }

    public int FreeSpace() => Math.Max( 0, CapacitySpace - UsedSpace() );

    private static int SlotIndexFor(ResourceType t)
    {
        return t switch
        {
            ResourceType.Stellium => 0,
            ResourceType.Plasma   => 1,
            ResourceType.Alloy    => 2,
            _ => 0
        };
    }

    // ====== API (Ressources) — capacity-aware, 1 slot par type ======
    /// Retourne le reste non-ajouté si l'inventaire manque de place.
    public int AddResourceStacks( ResourceType type, int amount )
{
    if ( amount <= 0 ) return 0;

    int per = UnitSpace(type);
    int roomUnits = FreeSpace() / per;
    if ( roomUnits <= 0 ) return amount;

    int willTake = Math.Min(amount, roomUnits);

    // 1) chercher slot existant même type (hors hands)
    for (int i = 1; i < _slots.Length; i++)
    {
        ref var s = ref _slots[i];
        if ( s.HasItem && s.Count > 0 && s.Type == type )
        {
            s.Type = type;
            s.Count += willTake;
            s.HasItem = true;
            SlotsChanged?.Invoke();
            return amount - willTake;
        }
    }

    // 2) chercher slot vide (hors hands)
    for (int i = 1; i < _slots.Length; i++)
    {
        ref var s = ref _slots[i];
        if ( !s.HasItem || s.Count <= 0 )
        {
            s.Type = type;
            s.Count = willTake;
            s.HasItem = true;
            SlotsChanged?.Invoke();
            return amount - willTake;
        }
    }

    // pas de slot dispo
    return amount;
}


    /// Retire jusqu'à `amount` du slot `index`. Retourne la quantité retirée.
    public int RemoveFromSlot( int index, int amount )
    {
        if ( index < 0 || index >= _slots.Length ) return 0;
        ref var s = ref _slots[index];
        if ( amount <= 0 || s.Count <= 0 ) return 0;

        int take = Math.Min( s.Count, amount );
        s.Count -= take;
        if ( s.Count <= 0 )
        {
            s.HasItem = false;
            s.Count = 0;
        }

        if ( take > 0 ) SlotsChanged?.Invoke();
        return take;
    }
	
	    /// <summary>
    /// Ajoute jusqu'à 'amount' unités de la ressource 'type'
    /// dans le slot 'index', en respectant la capacité globale.
    /// Retourne la quantité effectivement ajoutée.
    /// - Si le slot est vide → on le remplit avec ce type.
    /// - Si le slot contient le même type → on augmente le Count.
    /// - Si le slot contient un autre type → on refuse (0).
    /// </summary>
    public int AddToSlot( int index, ResourceType type, int amount )
    {
        if ( _slots == null ) return 0;
        if ( index < 0 || index >= _slots.Length ) return 0;
        if ( amount <= 0 ) return 0;
		
		    // 🚫 Slot 0 = Hands → interdit
		if ( index == 0 )
			return 0;


        ref var s = ref _slots[index];

        // Si le slot contient un autre type → on ne fusionne pas
        if ( s.HasItem && s.Count > 0 && s.Type != type )
        {
            return 0;
        }

        // Combien de place il reste globalement ?
        int per = UnitSpace( type );
        if ( per <= 0 ) per = 1;

        int freeUnits = FreeSpace() / per;
        if ( freeUnits <= 0 )
            return 0;

        int willTake = Math.Min( amount, freeUnits );
        if ( willTake <= 0 ) return 0;

        // Appliquer au slot
        s.Type    = type;
        s.Count  += willTake;
        s.HasItem = (s.Count > 0);

        SlotsChanged?.Invoke();
        return willTake;
    }

	    /// <summary>
    /// Définit directement le contenu d'un slot (réorganisation interne).
    /// Ne change pas le total d'items (utilisé pour swap / merge via l'UI).
    /// </summary>
    public void SetSlotRaw( int index, ResourceType type, int count )
    {
        if ( _slots == null ) return;
        if ( index < 0 || index >= _slots.Length ) return;
		
		    // 🚫 Slot 0 = Hands → interdit
		if ( index == 0 )
			return;


        ref var s = ref _slots[index];

        if ( count <= 0 )
        {
            s.HasItem = false;
            s.Count   = 0;
            // On laisse s.Type tel quel, ça n'a pas vraiment d’importance si le slot est vide
        }
        else
        {
            s.Type    = type;
            s.Count   = count;
            s.HasItem = true;
        }

        SlotsChanged?.Invoke();
    }

	
	    /// <summary>
    /// Échange le contenu de deux slots d'inventaire (indices HUD / interactif).
    /// </summary>
   public void SwapSlots( int a, int b )
{
    if ( _slots == null ) return;
    if ( a < 0 || a >= _slots.Length ) return;
    if ( b < 0 || b >= _slots.Length ) return;
    if ( a == b ) return;

    // 🚫 On ne touche JAMAIS au slot Hands
    if ( a == 0 || b == 0 )
        return;


    // Échange simple des deux slots
    var tmp = _slots[a];
    _slots[a] = _slots[b];
    _slots[b] = tmp;

    // Notifie le HUD + panels
    SlotsChanged?.Invoke();
}



	
	
	
	
	

    // ====== DROP (inchangé) ======
    [Rpc.Host]
    public void DropOneHost( int selectedIndex )
    {
        var caller = Rpc.Caller ?? Connection.Local;
        if ( caller is null ) return;

        if ( Network == null || Network.Owner != caller ) return;

        int idx = selectedIndex.Clamp( 0, SlotCount - 1 );
        var s = _slots[idx];
        if ( s.Count <= 0 ) return;

        var taken = RemoveFromSlot( idx, 1 );
        if ( taken <= 0 ) return;

        RemoveFromSlotOwner( idx, taken );
        SpawnPickupInFrontOf( caller, s.Type, taken );
    }

    [Rpc.Owner]
    public void RemoveFromSlotOwner( int index, int amount )
    {
        if ( amount <= 0 ) return;

        if ( Networking.IsHost )
        {
            SlotsChanged?.Invoke();
            return;
        }

        _ = RemoveFromSlot( index, amount );
        SlotsChanged?.Invoke();
    }
	
	
	[Rpc.Owner]
    public void AddToSlotOwner( int index, ResourceType type, int amount )
    {
        if ( amount <= 0 ) return;

        if ( Networking.IsHost )
        {
            // Le host a déjà appliqué AddToSlot côté serveur.
            SlotsChanged?.Invoke();
            return;
        }

        _ = AddToSlot( index, type, amount );
        SlotsChanged?.Invoke();
    }


    private void SpawnPickupInFrontOf( Connection conn, ResourceType type, int amount )
    {
        try
        {
            var ps = Scene?.GetAllComponents<PlayerState>()
                           ?.FirstOrDefault(p => p != null && p.Network != null && p.Network.Owner == conn);
            if ( ps == null ) return;

            var trPlayer = ps.GameObject.Transform.World;
            var spawnPos = trPlayer.Position + trPlayer.Rotation.Forward * 48f + Vector3.Up * 8f;

            var go = new GameObject( true, $"pickup_{type}" );
            go.Transform.World = new Transform( spawnPos, Rotation.Identity );

            var p = go.Components.Create<ResourcePickup>();
            p.Type = type;
            p.Amount = amount;

            go.NetworkSpawn(); // owned par le serveur
        }
        catch ( Exception ex )
        {
            Log.Warning( $"[Drop] Spawn pickup erreur: {ex.Message}" );
        }
    }

    // ====== Ponts serveur ⇄ client (dispenser / pickups) ======
    public void AddResourceServer( Connection owner, ResourceType t, int amount )
    {
        if ( !Networking.IsHost || owner is null || amount <= 0 ) return;

        int remain = AddResourceStacks( t, amount );
        int actuallyAdded = amount - remain;
        if ( actuallyAdded <= 0 ) return;

        AddResourceClient( owner.Id.ToString(), t, actuallyAdded );
    }
	
	// Retourne la quantité effectivement ajoutée (0 si inventaire plein).
	public int AddResourceServerReturnAccepted( Connection owner, ResourceType t, int amount )
	{
		if ( !Networking.IsHost || owner is null || amount <= 0 ) return 0;

		int remain = AddResourceStacks( t, amount );
		int accepted = amount - remain;

		if ( accepted > 0 )
		{
			// notifier le client “owner”
			AddResourceClient( owner.Id.ToString(), t, accepted );
		}

		return accepted;
	}

	
	
	
	

    [Rpc.Broadcast]
    public void AddResourceClient( string connectionId, ResourceType t, int actuallyAdded )
    {
        if ( actuallyAdded <= 0 ) return;

        var local = Connection.Local;
        if ( local == null ) return;

        var isForMe = (local.Id.ToString() == connectionId);

        if ( Networking.IsHost && isForMe )
        {
            SlotsChanged?.Invoke();
            return;
        }

        if ( isForMe )
        {
            _ = AddResourceStacks( t, actuallyAdded );
            SlotsChanged?.Invoke();
        }
    }

    public IReadOnlyList<Slot> GetSlots() => _slots;

    public IReadOnlyDictionary<ResourceType, int> GetAllResourcesSnapshot()
    {
        var map = new Dictionary<ResourceType, int>();
        for ( int i = 0; i < _slots.Length; i++ )
        {
            ref var s = ref _slots[i];
            if ( s.Count <= 0 ) continue;
            map[s.Type] = map.TryGetValue( s.Type, out var v ) ? v + s.Count : s.Count;
        }
        return map;
    }
}
