using System;

namespace Astrofront;

/// <summary>
/// Contexte global partagé entre tous les panels UI.
/// Permet de “tenir en main” un item et son origine (loot panel ou inventaire).
/// </summary>
public static class UiDragContext
{
    /// <summary>Type de la ressource en main.</summary>
    public static ResourceType? HeldType { get; private set; }

    /// <summary>Quantité en main.</summary>
    public static int HeldAmount { get; private set; }

    /// <summary>True si la main fantôme contient quelque chose.</summary>
    public static bool HasItem => HeldType.HasValue && HeldAmount > 0;

    /// <summary>D’où vient l’item (loot panel ou inventaire).</summary>
    public static UiDragSourceKind SourceKind { get; private set; } = UiDragSourceKind.None;

    /// <summary>Index du slot source (loot ou inventaire).</summary>
    public static int SourceIndex { get; private set; } = -1;


    /// <summary>
    /// Commence à tenir en main un stack.
    /// </summary>
    public static void BeginHold(ResourceType type, int amount, UiDragSourceKind kind, int sourceIndex)
    {
        HeldType = type;
        HeldAmount = amount;
        SourceKind = kind;
        SourceIndex = sourceIndex;

        Log.Info($"[UiDragContext] BeginHold type={type}, amount={amount}, source={kind}, idx={sourceIndex}");
    }


    /// <summary>
    /// Retire une quantité de la main (clic secondaire).
    /// </summary>
    public static void TakeFromHand(int amount)
    {
        if (!HasItem) return;

        HeldAmount -= amount;
        if (HeldAmount <= 0)
        {
            Clear();
        }
        else
        {
            Log.Info($"[UiDragContext] TakeFromHand left={HeldAmount}");
        }
    }


    /// <summary>
    /// Vide complètement la main fantôme.
    /// </summary>
    public static void Clear()
    {
        Log.Info("[UiDragContext] Clear()");

        HeldType = null;
        HeldAmount = 0;
        SourceKind = UiDragSourceKind.None;
        SourceIndex = -1;
    }
}


/// <summary>
/// Origine d’un item tenu en main.
/// Permet de savoir s’il faut retirer du monde ou non.
/// </summary>
public enum UiDragSourceKind
{
    None = 0,
    LootPanel = 1,
    Inventory = 2
}
