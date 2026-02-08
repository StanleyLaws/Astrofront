using Sandbox;
using System;
using System.Collections.Generic;

namespace Astrofront;

/// Registry global des motors.
/// Permet aux modes de déclarer des motors par ID sans référencer les classes partout.
public static class MovementMotorRegistry
{
	private static readonly Dictionary<string, Func<IMovementMotor>> _factories
		= new( StringComparer.OrdinalIgnoreCase );

	/// Enregistre / remplace un motor.
	public static void Register( string id, Func<IMovementMotor> factory )
	{
		if ( string.IsNullOrWhiteSpace( id ) )
			throw new ArgumentException( "Motor id is null/empty", nameof( id ) );

		if ( factory == null )
			throw new ArgumentNullException( nameof( factory ) );

		_factories[id] = factory;
	}

	/// Tente de créer un motor depuis un id.
	public static bool TryCreate( string id, out IMovementMotor motor )
	{
		motor = null;

		if ( string.IsNullOrWhiteSpace( id ) )
			return false;

		if ( !_factories.TryGetValue( id, out var factory ) || factory == null )
			return false;

		motor = factory();
		return motor != null;
	}

	/// Crée un motor depuis un id (throw si introuvable).
	public static IMovementMotor CreateOrThrow( string id )
	{
		if ( TryCreate( id, out var motor ) )
			return motor;

		throw new KeyNotFoundException( $"No Movement motor registered for id '{id}'." );
	}
}
