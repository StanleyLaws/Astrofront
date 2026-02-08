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
}
