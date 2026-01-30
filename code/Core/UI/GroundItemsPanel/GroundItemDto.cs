using System;

namespace Astrofront;

[Serializable]
public struct GroundItemDto
{
    public Guid PickupId;
    public string ItemId;
    public int Amount;

    // ✅ Pour compat avec anciens appels: new GroundItemDto(itemId, amount)
    public GroundItemDto( string itemId, int amount )
    {
        PickupId = Guid.Empty;
        ItemId = itemId;
        Amount = amount;
    }

    // ✅ Optionnel (si tu veux aussi pouvoir remplir l'id directement)
    public GroundItemDto( Guid pickupId, string itemId, int amount )
    {
        PickupId = pickupId;
        ItemId = itemId;
        Amount = amount;
    }
}
