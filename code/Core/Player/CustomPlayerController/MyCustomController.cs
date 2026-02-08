using Sandbox;
using System;

namespace Astrofront;

public sealed class MyCustomController : Component
{
	[Property, Group("Refs")] public CameraComponent Camera { get; set; }
	[Property, Group("Refs")] public CharacterController CharacterController { get; set; }
	[Property, Group("Refs")] public PlayerMovementInput MovementInput { get; set; }

	private IMovementMotor _motor;

	// ✅ Motor par défaut (configurable par Rules/Prefab)
	[Property, Group("Motor")] public string DefaultMotorId { get; set; } = "walk";

	// --------------------
	// Orientation policy
	// --------------------
	public enum ThirdPersonFacingMode
	{
		CameraYaw = 0,
		MoveDirection = 1
	}

	[Property, Group("Orientation")] public bool AlignToCameraYaw { get; set; } = true;
	[Property, Group("Orientation")] public bool FirstPersonAlwaysAlignYaw { get; set; } = true;
	[Property, Group("Orientation")] public ThirdPersonFacingMode ThirdPersonFacing { get; set; } = ThirdPersonFacingMode.CameraYaw;
	[Property, Group("Orientation")] public float AlignRotateSpeed { get; set; } = 12f;

	// ---------------- Capsule ----------------
	[Property, Group("Capsule")] public float StandRadius { get; set; } = 16f;
	[Property, Group("Capsule")] public float StandHeight { get; set; } = 72f;
	[Property, Group("Capsule")] public float StepHeight { get; set; } = 18f;

	// ---------------- Vitesse ----------------
	[Property, Group("Speed")] public float WalkSpeed { get; set; } = 220f;
	[Property, Group("Speed")] public float SprintSpeed { get; set; } = 320f;
	[Property, Group("Speed")] public float SlowWalkSpeed { get; set; } = 120f;

	[Property, Group("Speed")] public float GroundFriction { get; set; } = 6f;
	[Property, Group("Speed")] public float StopSpeed { get; set; } = 120f;

	[Property, Group("Speed")] public float Gravity { get; set; } = 900f;
	[Property, Group("Speed")] public float Acceleration { get; set; } = 10f;

	// ---------------- Jump tuning ----------------
	[Property, Group("Jump")] public float JumpSpeed { get; set; } = 360f;
	[Property, Group("Jump")] public float CoyoteTime { get; set; } = 0.12f;
	[Property, Group("Jump")] public float JumpBuffer { get; set; } = 0.12f;

	// ---------------- Duck ----------------
	[Property, Group("Duck")] public float DuckInSpeed { get; set; } = 12f;
	[Property, Group("Duck")] public float DuckOutSpeed { get; set; } = 40f;
	[Property, Group("Duck")] public float DuckRadiusScale { get; set; } = 0.80f;
	[Property, Group("Duck")] public float DuckHeightScale { get; set; } = 0.60f;
	[Property, Group("Duck")] public float DuckSpeedMultiplier { get; set; } = 0.55f;

	private float _duck;

	// Jump latch (Update -> FixedUpdate)
	private bool _jumpLatched;

	public bool IsGrounded => CharacterController?.IsOnGround ?? false;
	public Vector3 Velocity => CharacterController?.Velocity ?? default;
	public float Radius => CharacterController?.Radius ?? StandRadius;

	public float DuckAmount => _duck;
	public bool IsDucking => _duck > 0.5f;

	protected override void OnStart()
	{
		EnsureRefs();

		if ( CharacterController == null )
		{
			Log.Warning( "[MyCustomController] Missing CharacterController." );
			return;
		}

		// (re)apply capsule base
		CharacterController.Radius = StandRadius;
		CharacterController.Height = StandHeight;
		CharacterController.StepHeight = StepHeight;
		CharacterController.Acceleration = Acceleration;
		CharacterController.UseCollisionRules = true;

		// ✅ Si aucun motor n'a été choisi avant OnStart, on prend DefaultMotorId via registry
		if ( _motor == null )
		{
			if ( !TrySetMotorByIdInternal( DefaultMotorId, activateNow: true ) )
			{
				// Fallback hard si registry pas prêt (ou id invalide)
				_motor = new WalkMotor();
				_motor.OnActivated( BuildContext( false ) );
				Log.Warning( $"[MyCustomController] DefaultMotorId '{DefaultMotorId}' not found. Fallback WalkMotor()." );
			}
		}
		else
		{
			// Motor déjà assigné tôt → activer maintenant
			_motor.OnActivated( BuildContext( false ) );
		}
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		EnsureRefs();

		if ( MovementInput == null ) return;

		if ( MovementInput.CanGameplayInput && MovementInput.JumpPressedThisFrame )
			_jumpLatched = true;

		if ( !MovementInput.CanGameplayInput )
			_jumpLatched = false;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		EnsureRefs();

		if ( CharacterController == null ) return;
		if ( MovementInput == null ) return;
		if ( _motor == null ) return;

		UpdateDuck();
		ApplyCapsuleFromDuck();
		UpdateOrientation();

		bool jumpPressed = _jumpLatched && MovementInput.CanGameplayInput;
		var ctx = BuildContext( jumpPressed );

		_motor.Step( ctx );

		_jumpLatched = false;
	}

	private void EnsureRefs()
	{
		CharacterController ??= Components.Get<CharacterController>( FindMode.InSelf );
		MovementInput ??= Components.Get<PlayerMovementInput>( FindMode.EverythingInSelfAndAncestors );
	}

	// =========================================================
	// Motor API (registry-friendly)
	// =========================================================

	/// Change le motor actif à partir d'un ID enregistré dans MovementMotorRegistry.
	public bool SetMotorById( string motorId )
	{
		return TrySetMotorByIdInternal( motorId, activateNow: true );
	}

	/// Retour au motor "walk" Core.
	public void UseWalkMotor()
	{
		// Si registry pas prêt, fallback interne de sécurité
		if ( !SetMotorById( "walk" ) )
			SetMotor( new WalkMotor() );
	}

	/// API low-level : change le motor actif (safe même si appelé avant OnStart()).
	public void SetMotor( IMovementMotor newMotor )
	{
		if ( newMotor == null )
			return;

		EnsureRefs();

		// Si refs pas prêtes : on mémorise, OnStart activera.
		if ( CharacterController == null )
		{
			_motor = newMotor;
			return;
		}

		var ctx = BuildContext( false );

		_motor?.OnDeactivated( ctx );

		_motor = newMotor;
		_motor.OnActivated( ctx );
	}

	private bool TrySetMotorByIdInternal( string motorId, bool activateNow )
	{
		if ( string.IsNullOrWhiteSpace( motorId ) )
			return false;

		// Création depuis le registry
		if ( !MovementMotorRegistry.TryCreate( motorId, out var motor ) || motor == null )
			return false;

		EnsureRefs();

		// Si on ne peut pas activer (refs pas prêtes), on stocke juste
		if ( CharacterController == null || !activateNow )
		{
			_motor = motor;
			return true;
		}

		var ctx = BuildContext( false );

		_motor?.OnDeactivated( ctx );

		_motor = motor;
		_motor.OnActivated( ctx );

		return true;
	}

	// =========================================================
	// Duck / Capsule / Orientation
	// =========================================================

	private void UpdateDuck()
	{
		bool wantDuck = MovementInput.CanGameplayInput && MovementInput.DuckHeld;
		float target = wantDuck ? 1f : 0f;

		float speed = (target > _duck) ? DuckInSpeed : DuckOutSpeed;
		_duck = _duck.LerpTo( target, speed * Time.Delta );
	}

	private void ApplyCapsuleFromDuck()
	{
		float radius = MathX.Lerp( StandRadius, StandRadius * DuckRadiusScale, _duck );
		float height = MathX.Lerp( StandHeight, StandHeight * DuckHeightScale, _duck );
		height = MathF.Max( height, radius * 2f + 1f );

		CharacterController.Radius = radius;
		CharacterController.Height = height;
		CharacterController.StepHeight = StepHeight;
		CharacterController.Acceleration = Acceleration;
	}

	private void UpdateOrientation()
	{
		if ( !AlignToCameraYaw ) return;
		if ( Camera is null ) return;
		if ( MovementInput == null ) return;
		if ( !MovementInput.CanGameplayInput ) return;

		var camFwdFlat = Camera.WorldRotation.Forward.WithZ( 0 );
		if ( camFwdFlat.IsNearlyZero() ) return;
		var cameraYawRot = Rotation.LookAt( camFwdFlat.Normal, Vector3.Up );

		bool isFirstPerson = false;
		var camBrain = Camera.Components.Get<MyCustomControllerCamera>( FindMode.EverythingInSelfAndAncestors );
		if ( camBrain != null && camBrain.Mode == MyCustomControllerCamera.CameraMode.FirstPerson )
			isFirstPerson = true;

		if ( isFirstPerson && FirstPersonAlwaysAlignYaw )
		{
			WorldRotation = Rotation.Lerp( WorldRotation, cameraYawRot, AlignRotateSpeed * Time.Delta );
			return;
		}

		if ( ThirdPersonFacing == ThirdPersonFacingMode.CameraYaw )
		{
			if ( MovementInput.MoveAxis.LengthSquared > 1e-6f )
				WorldRotation = Rotation.Lerp( WorldRotation, cameraYawRot, AlignRotateSpeed * Time.Delta );
			return;
		}

		var move2 = MovementInput.MoveAxis;
		if ( move2.LengthSquared <= 1e-6f ) return;

		var fwd = cameraYawRot.Forward;
		var right = fwd.Cross( Vector3.Up ).Normal;
		var wishDir = (right * move2.x + fwd * move2.y).WithZ( 0 );

		if ( !wishDir.IsNearlyZero() )
		{
			var targetYaw = Rotation.LookAt( wishDir.Normal, Vector3.Up );
			WorldRotation = Rotation.Lerp( WorldRotation, targetYaw, AlignRotateSpeed * Time.Delta );
		}
	}

	// =========================================================
	// Context builder
	// =========================================================

	private MovementMotorContext BuildContext( bool jumpPressedLatched )
	{
		var moveAxis = MovementInput?.MoveAxis ?? Vector2.Zero;
		bool duckHeld = MovementInput?.DuckHeld ?? false;
		bool slowHeld = MovementInput?.SlowWalkHeld ?? false;
		bool sprintHeld = MovementInput?.SprintHeld ?? false;

		float baseSpeed = WalkSpeed;
		if ( slowHeld ) baseSpeed = SlowWalkSpeed;
		else if ( sprintHeld ) baseSpeed = SprintSpeed;

		return new MovementMotorContext
		{
			Controller = CharacterController,
			Camera = Camera,

			MoveAxis = moveAxis,
			JumpPressed = jumpPressedLatched,
			JumpHeld = jumpPressedLatched, // placeholder
			DuckHeld = duckHeld,

			DesiredSpeed = baseSpeed,
			SpeedMultiplier = MathX.Lerp( 1f, DuckSpeedMultiplier, _duck ),

			Gravity = Gravity,
			Acceleration = Acceleration,
			Friction = GroundFriction,
			StopSpeed = StopSpeed,

			JumpSpeed = JumpSpeed,
			CoyoteTime = CoyoteTime,
			JumpBuffer = JumpBuffer,

			IsGrounded = CharacterController?.IsOnGround ?? false,
			DeltaTime = Time.Delta
		};
	}
}
