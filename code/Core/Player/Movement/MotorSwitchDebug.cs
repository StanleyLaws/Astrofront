using Sandbox;

namespace Astrofront;

/// Debug helper: toggle entre 2 motors via leurs IDs (registry).
/// À attacher sur le root du player.
public sealed class MotorSwitchDebug : Component
{
	[Property, Group("Refs")] public MyCustomController Controller { get; set; }

	// Input Actions existantes
	[Property, Group("Input")] public string ToggleAction { get; set; } = "Test";
	[Property, Group("Input")] public string ForceSecondAction { get; set; } = "Flashlight";

	// ✅ IDs registry
	[Property, Group("Motors")] public string FirstMotorId { get; set; } = "walk";
	[Property, Group("Motors")] public string SecondMotorId { get; set; } = "astrofront_fly";

	[Property, Group("Debug")] public bool Logs { get; set; } = true;

	private bool _second;

	protected override void OnStart()
	{
		if ( Controller == null )
		{
			foreach ( var c in Components.GetAll<MyCustomController>( FindMode.EverythingInSelfAndDescendants ) )
			{
				if ( c.CharacterController != null || c.Components.Get<CharacterController>( FindMode.InSelf ) != null )
				{
					Controller = c;
					break;
				}
			}

			if ( Controller == null )
			{
				foreach ( var c in Components.GetAll<MyCustomController>( FindMode.EverythingInSelfAndAncestors ) )
				{
					if ( c.CharacterController != null || c.Components.Get<CharacterController>( FindMode.InSelf ) != null )
					{
						Controller = c;
						break;
					}
				}
			}
		}

		if ( Logs )
			Log.Info( Controller != null
				? $"[MotorSwitchDebug] Bound controller on GO='{Controller.GameObject?.Name}'"
				: "[MotorSwitchDebug] No MyCustomController found to bind." );
	}

	protected override void OnUpdate()
	{
		if ( Controller == null ) return;

		// input seulement owner local
		if ( !IsLocalOwner() )
			return;

		if ( UiModalController.IsUiLockedLocal )
			return;

		if ( !string.IsNullOrEmpty( ToggleAction ) && Input.Pressed( ToggleAction ) )
		{
			_second = !_second;
			Apply();
		}

		if ( !string.IsNullOrEmpty( ForceSecondAction ) && Input.Pressed( ForceSecondAction ) )
		{
			_second = true;
			Apply();
		}
	}

	private bool IsLocalOwner()
	{
		if ( Network != null && Connection.Local != null )
			return Network.Owner == Connection.Local;

		return !IsProxy && Connection.Local != null;
	}

	private void Apply()
	{
		string id = _second ? SecondMotorId : FirstMotorId;

		bool ok = Controller.SetMotorById( id );

		if ( Logs )
			Log.Info( ok
				? $"[MotorSwitchDebug] -> '{id}'"
				: $"[MotorSwitchDebug] FAILED -> '{id}' (not registered?)" );
	}
}
