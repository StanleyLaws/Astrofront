using Sandbox;

namespace Astrofront;

/// <summary>
/// Contexte UI + permissions gameplay "soft" pilotées par les Rules.
/// - Agnostique du controller (S&box / custom)
/// - Décidé par le HOST puis sync aux clients
///
/// Noms demandés :
/// - vitalbar
/// - invhud
/// - inventorymanagepanel
/// </summary>
public sealed class PlayerUiContext : Component
{
	// ===== UI visibility / availability =====

	/// <summary>HUD vital (health/energy/weight...).</summary>
	[Sync( SyncFlags.FromHost )]
	public bool vitalbar { get; private set; } = true;

	/// <summary>HUD inventaire (hotbar / slots molette).</summary>
	[Sync( SyncFlags.FromHost )]
	public bool invhud { get; private set; } = true;

	/// <summary>Inventaire interactif (InventoryManagePanel).</summary>
	[Sync( SyncFlags.FromHost )]
	public bool inventorymanagepanel { get; private set; } = true;

	// ===== Gameplay permissions (mode rules) =====

	/// <summary>PVP autorisé dans ce mode.</summary>
	[Sync( SyncFlags.FromHost )]
	public bool pvp { get; private set; } = true;

	/// <summary>Interaction Use autorisée (F / Use).</summary>
	[Sync( SyncFlags.FromHost )]
	public bool use { get; private set; } = true;

	// ===== Host API =====

	/// <summary>
	/// Définit les permissions UI (HOST only).
	/// </summary>
	public void SetUiHost( bool vitalbarEnabled, bool invhudEnabled, bool inventoryManagePanelEnabled )
	{
		if ( !Networking.IsHost ) return;

		vitalbar = vitalbarEnabled;
		invhud = invhudEnabled;
		inventorymanagepanel = inventoryManagePanelEnabled;
	} 

	/// <summary>
	/// Définit les permissions gameplay (HOST only).
	/// </summary>
	public void SetGameplayHost( bool pvpEnabled, bool useEnabled )
	{
		if ( !Networking.IsHost ) return;

		pvp = pvpEnabled;
		use = useEnabled;
	}

	/// <summary>
	/// Helpers de lecture côté client/serveur (agnostiques).
	/// </summary>
	public bool IsInventoryManageAllowed => inventorymanagepanel;
	public bool IsInvHudAllowed => invhud;
	public bool IsVitalbarAllowed => vitalbar;
	public bool IsPvpAllowed => pvp;
	public bool IsUseAllowed => use;
}
