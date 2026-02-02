using Sandbox;

namespace Astrofront;

/// <summary>
/// Ajoute un 3e mode "SlowWalk" au PlayerController S&box, sans réécrire le controller.
/// - Le PlayerController gère déjà Walk/Run.
/// - Ici on n'ajoute que SlowWalk (marche lente) via une action dédiée.
///
/// Principe:
/// - Quand SlowWalk est maintenu, on force temporairement la "vitesse de marche"
///   à SlowWalkSpeed.
/// - Quand SlowWalk est relâché, on restaure la WalkSpeed du mode.
///
/// IMPORTANT:
/// - Ce component NE décide pas des valeurs finales d'un mini-jeu.
///   Les Rules doivent configurer SlowWalkSpeed et la WalkSpeed/RunSpeed du mode.
/// </summary>
public sealed class SboxMovementModeController : Component
{
	/// <summary>Référence au PlayerController (fallback auto si non assigné).</summary>
	[Property] public PlayerController Controller { get; set; }

	/// <summary>Action input pour la marche lente (à créer dans l'éditeur Input Actions).</summary>
	[Property] public string SlowWalkButton { get; set; } = "SlowWalk";

	/// <summary>Vitesse de marche lente (configurée par les Rules du mode).</summary>
	[Property] public float SlowWalkSpeed { get; set; } = 120f;

	// Cache local pour restaurer la WalkSpeed du mode
	private float _savedWalkSpeed;
	private bool _hadSaved;
	private bool _isSlowWalking;

	protected override void OnStart()
	{
		Controller ??= Components.Get<PlayerController>( FindMode.EverythingInSelfAndDescendants );

		if ( Controller == null )
			Log.Warning( "[SboxMovementModeController] No PlayerController found. Component will do nothing." );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		if ( Controller == null ) return;

		// Si UI modale ouverte, on ne change rien ici :
		// CorePlayerUiLock s'occupe déjà de couper l'input et la wish velocity.
		if ( UiModalController.IsUiLockedLocal )
			return;

		bool wantSlow = Input.Down( SlowWalkButton );

		// Entrée en slow-walk
		if ( wantSlow && !_isSlowWalking )
		{
			_isSlowWalking = true;

			// Sauvegarder la WalkSpeed actuelle (celle posée par les Rules du mode)
			_savedWalkSpeed = Controller.WalkSpeed;
			_hadSaved = true;

			// Forcer la vitesse lente
			Controller.WalkSpeed = SlowWalkSpeed;
		}
		// Sortie du slow-walk
		else if ( !wantSlow && _isSlowWalking )
		{
			_isSlowWalking = false;

			// Restaurer la WalkSpeed du mode
			if ( _hadSaved )
				Controller.WalkSpeed = _savedWalkSpeed;
		}
	}

	/// <summary>
	/// Permet aux Rules de mettre à jour la vitesse lente sans toucher à l'inspector.
	/// </summary>
	public void SetSlowWalkSpeed( float speed )
	{
		SlowWalkSpeed = speed;

		// Si on est actuellement en slow-walk, appliquer immédiatement.
		if ( _isSlowWalking && Controller != null )
			Controller.WalkSpeed = SlowWalkSpeed;
	}

	/// <summary>
	/// Si les Rules changent WalkSpeed pendant que le joueur slow-walk,
	/// elles peuvent appeler cette méthode pour que la restauration soit correcte.
	/// </summary>
	public void RefreshSavedWalkSpeedFromController()
	{
		if ( Controller == null ) return;

		_savedWalkSpeed = Controller.WalkSpeed;
		_hadSaved = true;
	}
}
