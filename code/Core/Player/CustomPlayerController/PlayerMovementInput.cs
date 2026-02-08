using Sandbox;

namespace Astrofront;

/// Lit les inputs de mouvement (clavier + analog) et applique les règles globales:
/// - si UI modale ouverte -> aucun input gameplay
/// - Jump : expose un "pressed this frame" (edge) utilisable pour buffer/coyote dans le controller
///
/// Ce composant ne fait PAS de physique. Il ne fait PAS de règles de gameplay.
/// Il ne fait que lire et normaliser les inputs.
public sealed class PlayerMovementInput : Component
{
	[Property, Group("Gate")] public bool BlockWhenUiLocked { get; set; } = true;

	// --- Etat courant (mis à jour en Update) ---
	public bool CanGameplayInput { get; private set; }

	/// Axe forward/back (-1..1)
	public float AxisForward { get; private set; }

	/// Axe right/left (-1..1) -> +1 = droite
	public float AxisRight { get; private set; }

	/// Vecteur 2D normalisé dans l’espace input (Right, Forward), longueur <= 1
	public Vector2 MoveAxis { get; private set; }

	/// Jump press "edge" (1 frame)
	public bool JumpPressedThisFrame { get; private set; }

	/// Duck maintenu
	public bool DuckHeld { get; private set; }

	/// Sprint maintenu (optionnel — action string configurable)
	public bool SprintHeld { get; private set; }

	/// SlowWalk maintenu (optionnel — action string configurable)
	public bool SlowWalkHeld { get; private set; }

	// --- Actions configurables (Core agnostique des mappings) ---
	[Property, Group("Actions")] public string SprintAction { get; set; } = "Run";
	[Property, Group("Actions")] public string SlowWalkAction { get; set; } = "SlowWalk";

	protected override void OnUpdate()
	{
		// Proxy: on ne lit jamais les inputs.
		if ( IsProxy )
		{
			Clear();
			return;
		}

		CanGameplayInput = !(BlockWhenUiLocked && UiModalController.IsUiLockedLocal);

		if ( !CanGameplayInput )
		{
			Clear();
			return;
		}

		// --- Axes digital (WASD) ---
		bool f = Input.Down( InputActions.Forward );
		bool b = Input.Down( InputActions.Backward );
		bool l = Input.Down( InputActions.Left );
		bool r = Input.Down( InputActions.Right );

		AxisForward = (f ? 1f : 0f) - (b ? 1f : 0f);
		AxisRight   = (r ? 1f : 0f) - (l ? 1f : 0f);

		// --- Analog (gamepad) ---
		var analog = Input.AnalogMove;
		float aForward = analog.x;
		float aRight   = -analog.y; // convention: Y = left → right = -Y

		AxisForward = PickDominant( AxisForward, aForward );
		AxisRight   = PickDominant( AxisRight,   aRight );

		// --- MoveAxis normalisé ---
		var v = new Vector2( AxisRight, AxisForward );
		if ( v.Length > 1f ) v = v.Normal;
		MoveAxis = v;

		// --- Buttons ---
		JumpPressedThisFrame = Input.Pressed( InputActions.Jump );
		DuckHeld             = Input.Down( InputActions.Duck );

		// Actions optionnelles basées sur string (ne crash pas si non mappées)
		SprintHeld   = !string.IsNullOrEmpty( SprintAction )   && Input.Down( SprintAction );
		SlowWalkHeld = !string.IsNullOrEmpty( SlowWalkAction ) && Input.Down( SlowWalkAction );
	}

	private static float PickDominant( float digital, float analog )
	{
		return (System.MathF.Abs( analog ) > System.MathF.Abs( digital )) ? analog : digital;
	}

	private void Clear()
	{
		CanGameplayInput = false;
		AxisForward = 0f;
		AxisRight = 0f;
		MoveAxis = Vector2.Zero;

		JumpPressedThisFrame = false;
		DuckHeld = false;
		SprintHeld = false;
		SlowWalkHeld = false;
	}
}
