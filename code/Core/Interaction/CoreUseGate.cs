using Sandbox;

namespace Astrofront;

/// <summary>
/// Point d'entrée Core pour l'action Use.
/// Les systèmes gameplay doivent passer par ici
/// pour respecter les permissions du mode.
/// pour utliser : if ( Input.Pressed(InputActions.Use) && CoreUseGate.CanUse(player) ){ ...interaction }
/// </summary>
public static class CoreUseGate
{
	public static bool CanUse( GameObject player )
	{
		if ( player == null ) return false;

		var ctx = player.Components.Get<PlayerUiContext>( FindMode.EverythingInSelfAndDescendants );
		if ( ctx == null ) return true; // défaut permissif

		return ctx.use;
	}
}
