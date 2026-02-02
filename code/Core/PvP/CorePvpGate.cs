using Sandbox;

namespace Astrofront;

/// <summary>
/// Gate Core pour savoir si le PVP est autorisé pour un joueur.
/// Tous les systèmes de dégâts entre joueurs doivent passer par ici.

/// Comment utiliser :
/// Quand tu feras ton système d’armes / dégâts, au lieu de faire : victimState.AddHealthHost( -damage );
/// Tu feras : if ( CorePvpGate.CanDamage( attackerGameObject, victimGameObject ) ) 
/// { victimState.AddHealthHost( -damage );}

/// </summary>
public static class CorePvpGate
{
	public static bool CanDamage( GameObject attacker, GameObject victim )
	{
		if ( attacker == null || victim == null )
			return false;

		// Même joueur → pas de PVP
		if ( attacker == victim )
			return false;

		var ctx = attacker.Components.Get<PlayerUiContext>( FindMode.EverythingInSelfAndDescendants );
		if ( ctx == null )
			return true; // défaut permissif si pas de contexte

		return ctx.pvp;
	}
}
