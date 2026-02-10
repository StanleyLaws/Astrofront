using Sandbox;

namespace Astrofront;

/// Interface commune à tous les motors de déplacement.
/// Un motor ne lit PAS les inputs directement et ne connaît pas la scène.
/// Il reçoit un contexte et applique sa logique sur le CharacterController.
public interface IMovementMotor
{
	/// Appelé quand le motor devient actif
	void OnActivated( MovementMotorContext context );

	/// Appelé quand le motor est désactivé
	void OnDeactivated( MovementMotorContext context );

	/// Appelé chaque FixedUpdate
	void Step( MovementMotorContext context );

	/// Optionnel : permet au motor d'exposer des "hints" d'animation au CitizenAnimDriver
	/// (walk vs fly vs zeroG, style, etc.) sans coupler l'anim au mode de jeu.
	///
	/// Par défaut, un motor peut ignorer ça : le driver anim utilisera fallback (vitesse/grounded).
	void GetAnimHints( ref MovementMotorAnimHints hints );
}

/// Hints d'animation remplis par le motor actif (optionnel).
/// Le CitizenAnimDriver peut les consommer pour piloter citizen.vanmgrph de manière scalable.
public struct MovementMotorAnimHints
{
	/// True si le motor veut forcer un état "grounded" (ex: magboots) ; sinon laisse fallback.
	public bool OverrideGrounded;
	public bool Grounded;

	/// True si le motor veut forcer "moving" (ex: hover en fly) ; sinon fallback vitesse.
	public bool OverrideMoving;
	public bool Moving;

	/// Multiplieur global sur les vitesses envoyées à l'anim graph (wish/move speeds).
	/// 1 = normal. Utile pour slow-mo, lourdeur, etc.
	public float AnimSpeedMultiplier;

	/// Style de locomotion (mapping à définir dans CitizenAnimDriver).
	/// Ex: 0 = normal, 1 = fly, 2 = zeroG...
	public int MoveStyle;

	/// Etats spéciaux (bitmask si tu veux), ou simple int (suivant ton graph).
	public int SpecialMovementStates;

	/// Holdtype citizen (0 = none/unarmed, etc.) si un mode veut forcer une posture.
	public int HoldType;

	/// Indique si le mode est en "firstperson anim" (ex: bras séparés) — optionnel.
	public bool ForceFirstPersonFlag;
	public bool FirstPersonFlag;

	public static MovementMotorAnimHints Default => new MovementMotorAnimHints
	{
		OverrideGrounded = false,
		Grounded = false,
		OverrideMoving = false,
		Moving = false,
		AnimSpeedMultiplier = 1f,
		MoveStyle = 0,
		SpecialMovementStates = 0,
		HoldType = 0,
		ForceFirstPersonFlag = false,
		FirstPersonFlag = false
	};
}

/// Contexte passé aux motors.
/// Contient uniquement des données "moteur" et pas d’input brut.
public struct MovementMotorContext
{
	public CharacterController Controller;
	public CameraComponent Camera;

	// Données issues du PlayerMovementInput déjà filtrées
	public Vector2 MoveAxis;        // x=right, y=forward
	public bool JumpPressed;        // edge (this frame)
	public bool JumpHeld;           // hold (si dispo)
	public bool DuckHeld;

	// Paramètres décidés par policies (vitesse, modifs, etc.)
	public float DesiredSpeed;
	public float SpeedMultiplier;

	public float Gravity;
	public float Acceleration;
	public float Friction;
	public float StopSpeed;

	public float JumpSpeed;
	public float CoyoteTime;
	public float JumpBuffer;

	// Etats
	public bool IsGrounded;
	public float DeltaTime;

	// ✅ NEW: anim hints (remplis par le motor actif, consommés par l'AnimDriver)
	public MovementMotorAnimHints AnimHints;
}
