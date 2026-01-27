using Sandbox;

namespace Astrofront;

/// <summary>
/// Contexte global partagé entre tous les panels UI.
/// Permet de “tenir en main” un item et son origine (loot panel ou inventaire).
/// </summary>
public static class UiDragContext
{
    public static string HeldItemId { get; private set; } = "";
    public static int HeldAmount { get; private set; } = 0;

    public static bool HasItem => !string.IsNullOrEmpty( HeldItemId ) && HeldAmount > 0;

    public static UiDragSourceKind SourceKind { get; private set; } = UiDragSourceKind.None;
    public static int SourceIndex { get; private set; } = -1;

    public static void BeginHold( string itemId, int amount, UiDragSourceKind kind, int sourceIndex )
    {
        if ( string.IsNullOrEmpty( itemId ) || amount <= 0 )
            return;

        HeldItemId = itemId;
        HeldAmount = amount;
        SourceKind = kind;
        SourceIndex = sourceIndex;

        Log.Info( $"[UiDragContext] BeginHold item={itemId}, amount={amount}, source={kind}, idx={sourceIndex}" );
    }

    public static void TakeFromHand( int amount )
    {
        if ( !HasItem ) return;

        HeldAmount -= amount;
        if ( HeldAmount <= 0 )
            Clear();
        else
            Log.Info( $"[UiDragContext] TakeFromHand left={HeldAmount}" );
    }

    public static void Clear()
    {
        Log.Info( "[UiDragContext] Clear()" );

        HeldItemId = "";
        HeldAmount = 0;
        SourceKind = UiDragSourceKind.None;
        SourceIndex = -1;
    }
}

public enum UiDragSourceKind
{
    None = 0,
    LootPanel = 1,
    Inventory = 2
}
