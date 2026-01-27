using System;
using System.Collections.Generic;

namespace Astrofront;

/// <summary>
/// Point unique de "réconciliation" de la main fantôme (UiDragContext).
/// Les panels/services publient des snapshots (loot dispo, chest dispo, etc.)
/// et ce reconciler ajuste la main fantôme si la source n'est plus valide.
/// </summary>
public static class UiDragReconciler
{
	// Dernier état connu du loot autour du joueur local (client)
	private static readonly Dictionary<string, int> _lootAvailable = new(StringComparer.OrdinalIgnoreCase);
	private static bool _hasLootSnapshot = false;

	/// <summary>
	/// Snapshot loot (ItemId -> quantité totale visible autour).
	/// Appelé par GroundItemsService à chaque refresh.
	/// </summary>
	public static void UpdateLootAvailability( Dictionary<string, int> available )
	{
		_lootAvailable.Clear();

		if ( available != null )
		{
			foreach ( var kv in available )
			{
				if ( string.IsNullOrEmpty( kv.Key ) ) continue;
				if ( kv.Value <= 0 ) continue;

				_lootAvailable[kv.Key] = kv.Value;
			}
		}

		_hasLootSnapshot = true;

		// Dès qu'on reçoit un snapshot, on recale la main fantôme si elle vient du loot.
		ReconcileHeldItem();
	}

	/// <summary>
	/// Appel safe (tu peux l'appeler n'importe quand).
	/// Ajuste uniquement les mains fantômes qui viennent du LootPanel.
	/// </summary>
	public static void ReconcileHeldItem()
	{
		if ( !UiDragContext.HasItem )
			return;

		// On ne "touche" qu'à la main provenant du loot.
		if ( UiDragContext.SourceKind != UiDragSourceKind.LootPanel )
			return;

		// Pas de snapshot -> on ne peut rien conclure, donc on ne casse rien.
		if ( !_hasLootSnapshot )
			return;

		var heldId = UiDragContext.HeldItemId;
		var heldAmount = UiDragContext.HeldAmount;

		if ( string.IsNullOrEmpty( heldId ) || heldAmount <= 0 )
			return;

		_lootAvailable.TryGetValue( heldId, out var availableAmount );

		// Plus disponible -> on clear
		if ( availableAmount <= 0 )
		{
			UiDragContext.Clear();
			return;
		}

		// Disponible mais moins que ce qu'on "croit" tenir -> on clamp
		if ( availableAmount < heldAmount )
		{
			UiDragContext.TakeFromHand( heldAmount - availableAmount ); // réduit proprement, Clear() auto si 0
		}
	}
}
