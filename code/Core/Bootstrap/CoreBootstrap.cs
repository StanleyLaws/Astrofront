using Sandbox;

namespace Astrofront;

/// Point d'entrée unique d'initialisation du Core.
/// Objectif : enregistrer une fois les "registries" (motors, etc.) de manière fiable,
/// sans dépendre de SceneStartup (qui ne s'exécute pas dans ton setup).
public static class CoreBootstrap
{
	private static bool _initialized;

	/// Appel safe (idempotent) : peut être appelé 100 fois, ça n'initialise qu'une seule fois.
	public static void EnsureInitialized()
	{
		if ( _initialized )
			return; 

		_initialized = true;

		RegisterCoreMotors();

		Log.Info( "[CoreBootstrap] Core initialized." );
	}

	private static void RegisterCoreMotors()
	{
		// Motors disponibles globalement (Core)
		MovementMotorRegistry.Register( "walk", () => new WalkMotor() );
		MovementMotorRegistry.Register( "fly",  () => new FlyMotor() );

		Log.Info( "[CoreBootstrap] Registered core motors: walk, fly" );
	}
}
