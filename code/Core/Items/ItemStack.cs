namespace Astrofront;

public readonly struct ItemStack
{
	public readonly string ItemId;
	public readonly int Amount;

	public ItemStack( string itemId, int amount )
	{
		ItemId = itemId ?? string.Empty;
		Amount = amount;
	}

	public bool IsValid => !string.IsNullOrEmpty( ItemId ) && Amount > 0;
}
