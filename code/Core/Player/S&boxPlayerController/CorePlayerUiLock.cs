using Sandbox;

namespace Astrofront;

/// <summary>
/// Pont Core : applique le "UI lock" (UiModalController) au PlayerController S&box.
/// But: quand une UI modale est ouverte (inventaire/loot), le joueur ne doit plus bouger.
///
/// À mettre sur le même GameObject que PlayerController (player_core).
/// </summary>
public sealed class CorePlayerUiLock : Component
{
	[Property] public PlayerController Controller { get; set; }

	private bool _wasLocked;
	private bool _savedUseInputControls;
	private bool _savedUseLookControls;

	protected override void OnStart()
	{
		// Fallback si pas assigné dans l'inspector
		Controller ??= Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants );

		if ( Controller == null )
		{
			Log.Warning( "[CorePlayerUiLock] No PlayerController found. Component will do nothing." );
		}
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		if ( Controller == null ) return;

		// Source de vérité : une UI modale est-elle ouverte ?
		bool locked = UiModalController.IsUiLockedLocal;

		// Détection entrée/sortie du lock
		if ( locked != _wasLocked )
		{
			_wasLocked = locked;

			if ( locked )
			{
				// Sauvegarde des états actuels
				_savedUseInputControls = Controller.UseInputControls;
				_savedUseLookControls  = Controller.UseLookControls;

				// Couper l'input built-in du PlayerController
				Controller.UseInputControls = false;

				// Couper le look built-in (selon versions, ça peut être partiel)
				Controller.UseLookControls = false;

				// Stop "use pressing"
				Controller.StopPressing();

				// Neutraliser immédiatement
				Controller.WishVelocity = Vector3.Zero;
			}
			else
			{
				// Restaure
				Controller.UseInputControls = _savedUseInputControls;
				Controller.UseLookControls  = _savedUseLookControls;

				// Nettoyage
				Controller.WishVelocity = Vector3.Zero;
			}
		}

		// Tant que locké, on force la wish velocity à zéro (sécurité)
		if ( locked )
		{
			Controller.WishVelocity = Vector3.Zero;
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;
		if ( Controller == null ) return;

		// Sécurité côté physique aussi
		if ( UiModalController.IsUiLockedLocal )
		{
			Controller.WishVelocity = Vector3.Zero;
		}
	}
}
