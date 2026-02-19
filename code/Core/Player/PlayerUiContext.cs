using Sandbox;

namespace Astrofront;

/// <summary>
/// Contexte UI + permissions gameplay "soft" pilotées par les Rules.
/// - Agnostique du controller (S&box / custom)
/// - Décidé par le HOST puis sync aux clients
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

	// ===== View / Camera permissions (CORE FPS SYSTEM) =====

	/// <summary>Le joueur peut utiliser la vue première personne.</summary>
	[Sync( SyncFlags.FromHost )]
	public bool allowFirstPerson { get; private set; } = true;

	/// <summary>Le joueur peut utiliser la vue troisième personne.</summary>
	[Sync( SyncFlags.FromHost )]
	public bool allowThirdPerson { get; private set; } = true;

	/// <summary>Autorise le rendu des bras viewmodel en FPS.</summary>
	[Sync( SyncFlags.FromHost )]
	public bool allowViewModel { get; private set; } = true;

	/// <summary>Autorise l'affichage des jambes locales en FPS.</summary>
	[Sync( SyncFlags.FromHost )]
	public bool allowLegsInFirstPerson { get; private set; } = true;

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
	/// Définit les permissions de vue (FPS/TPS/ViewModel/Legs) — HOST only.
	/// </summary>
	public void SetViewHost( bool firstPerson, bool thirdPerson, bool viewModel, bool legsInFp )
	{
		if ( !Networking.IsHost ) return;

		allowFirstPerson = firstPerson;
		allowThirdPerson = thirdPerson;
		allowViewModel = viewModel;
		allowLegsInFirstPerson = legsInFp;
	}

	// ===== Helpers de lecture =====

	public bool IsInventoryManageAllowed => inventorymanagepanel;
	public bool IsInvHudAllowed => invhud;
	public bool IsVitalbarAllowed => vitalbar;
	public bool IsPvpAllowed => pvp;
	public bool IsUseAllowed => use;

	public bool CanUseFirstPerson => allowFirstPerson;
	public bool CanUseThirdPerson => allowThirdPerson;
	public bool CanUseViewModel => allowViewModel;
	public bool CanUseLegsInFirstPerson => allowLegsInFirstPerson;
}
