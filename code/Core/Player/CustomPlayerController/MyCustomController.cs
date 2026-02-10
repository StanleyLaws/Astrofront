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

	// ✅ Derniers hints anim produits (Motor + Policy)
	public MovementMotorAnimHints LastAnimHints { get; private set; } = MovementMotorAnimHints.Default;

	// =========================================================
	// ✅ Animation Policy (configurable par mode via Rules)
	// =========================================================
	[Property, Group("AnimPolicy")] public bool UseAnimationPolicy { get; set; } = true;
	[Property, Group("AnimPolicy")] public float PolicyAnimSpeedMultiplier { get; set; } = 1f;
	[Property, Group("AnimPolicy")] public int PolicyMoveStyleOverride { get; set; } = -1;
	[Property, Group("AnimPolicy")] public int PolicySpecialStatesOverride { get; set; } = -1;
	[Property, Group("AnimPolicy")] public int PolicyHoldTypeOverride { get; set; } = -1;
	[Property, Group("AnimPolicy")] public int PolicyForceFirstPerson { get; set; } = -1;
	[Property, Group("AnimPolicy")] public int PolicyForceGrounded { get; set; } = -1;
	[Property, Group("AnimPolicy")] public int PolicyForceMoving { get; set; } = -1;

	public void SetAnimSpeedMultiplier( float mul ) => PolicyAnimSpeedMultiplier = mul;
	public void ClearMoveStyleOverride() => PolicyMoveStyleOverride = -1;
	public void SetMoveStyleOverride( int style ) => PolicyMoveStyleOverride = style;
	public void ClearHoldTypeOverride() => PolicyHoldTypeOverride = -1;
	public void SetHoldTypeOverride( int holdType ) => PolicyHoldTypeOverride = holdType;

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

		CharacterController.Radius = StandRadius;
		CharacterController.Height = StandHeight;
		CharacterController.StepHeight = StepHeight;
		CharacterController.Acceleration = Acceleration;
		CharacterController.UseCollisionRules = true;

		if ( _motor == null )
		{
			if ( !TrySetMotorByIdInternal( DefaultMotorId, activateNow: true ) )
			{
				_motor = new WalkMotor();
				_motor.OnActivated( BuildContext( jumpPressedLatched:false ) );
				Log.Warning( $"[MyCustomController] DefaultMotorId '{DefaultMotorId}' not found. Fallback WalkMotor()." );
			}
		}
		else
		{
			_motor.OnActivated( BuildContext( jumpPressedLatched:false ) );
		}

		RebuildAnimHints();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		// ✅ plus besoin du latch maison : PlayerMovementInput le gère
		EnsureRefs();
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

		// ✅ Latch fiable Update->Fixed (et edge uniquement si gameplay input ok)
		bool jumpPressed = MovementInput.CanGameplayInput && MovementInput.JumpPressedLatched;

		var ctx = BuildContext( jumpPressed );

		// ✅ 1) Motor hints
		var hints = MovementMotorAnimHints.Default;
		_motor.GetAnimHints( ref hints );

		// ✅ 2) Apply mode policy overrides (si activé)
		if ( UseAnimationPolicy )
			ApplyAnimationPolicy( ref hints );

		// ✅ 3) Source de vérité
		LastAnimHints = hints;
		ctx.AnimHints = hints;

		_motor.Step( ctx );

		// ✅ consommer seulement après Step()
		MovementInput.ConsumeJumpPressedLatch();
	}

	private void EnsureRefs()
	{
		CharacterController ??= Components.Get<CharacterController>( FindMode.InSelf );
		MovementInput ??= Components.Get<PlayerMovementInput>( FindMode.EverythingInSelfAndAncestors );
	}

	private void RebuildAnimHints()
	{
		var hints = MovementMotorAnimHints.Default;
		_motor?.GetAnimHints( ref hints );

		if ( UseAnimationPolicy )
			ApplyAnimationPolicy( ref hints );

		LastAnimHints = hints;
	}

	private void ApplyAnimationPolicy( ref MovementMotorAnimHints hints )
	{
		float mul = (PolicyAnimSpeedMultiplier <= 0f) ? 1f : PolicyAnimSpeedMultiplier;
		hints.AnimSpeedMultiplier *= mul;

		if ( PolicyMoveStyleOverride >= 0 )
			hints.MoveStyle = PolicyMoveStyleOverride;

		if ( PolicySpecialStatesOverride >= 0 )
			hints.SpecialMovementStates = PolicySpecialStatesOverride;

		if ( PolicyHoldTypeOverride >= 0 )
			hints.HoldType = PolicyHoldTypeOverride;

		if ( PolicyForceFirstPerson == 0 || PolicyForceFirstPerson == 1 )
		{
			hints.ForceFirstPersonFlag = true;
			hints.FirstPersonFlag = (PolicyForceFirstPerson == 1);
		}

		if ( PolicyForceGrounded == 0 || PolicyForceGrounded == 1 )
		{
			hints.OverrideGrounded = true;
			hints.Grounded = (PolicyForceGrounded == 1);
		}

		if ( PolicyForceMoving == 0 || PolicyForceMoving == 1 )
		{
			hints.OverrideMoving = true;
			hints.Moving = (PolicyForceMoving == 1);
		}
	}

	// =========================================================
	// Motor API (registry-friendly)
	// =========================================================

	public bool SetMotorById( string motorId )
	{
		return TrySetMotorByIdInternal( motorId, activateNow: true );
	}

	public void UseWalkMotor()
	{
		if ( !SetMotorById( "walk" ) )
			SetMotor( new WalkMotor() );
	}

	public void SetMotor( IMovementMotor newMotor )
	{
		if ( newMotor == null )
			return;

		EnsureRefs();

		if ( CharacterController == null )
		{
			_motor = newMotor;
			return;
		}

		var ctx = BuildContext( jumpPressedLatched:false );

		_motor?.OnDeactivated( ctx );

		_motor = newMotor;
		_motor.OnActivated( ctx );

		RebuildAnimHints();
	}

	private bool TrySetMotorByIdInternal( string motorId, bool activateNow )
	{
		if ( string.IsNullOrWhiteSpace( motorId ) )
			return false;

		if ( !MovementMotorRegistry.TryCreate( motorId, out var motor ) || motor == null )
			return false;

		EnsureRefs();

		if ( CharacterController == null || !activateNow )
		{
			_motor = motor;
			return true;
		}

		var ctx = BuildContext( jumpPressedLatched:false );

		_motor?.OnDeactivated( ctx );

		_motor = motor;
		_motor.OnActivated( ctx );

		RebuildAnimHints();
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

		bool duckHeld   = MovementInput?.DuckHeld ?? false;
		bool jumpHeld   = MovementInput?.JumpHeld ?? false;
		bool slowHeld   = MovementInput?.SlowWalkHeld ?? false;
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
			JumpHeld = jumpHeld,
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
			DeltaTime = Time.Delta,

			AnimHints = LastAnimHints
		};
	}
}
