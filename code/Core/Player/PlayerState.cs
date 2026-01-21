using Sandbox;
using System;

namespace Astrofront;

public enum Team { None = -1, Red = 0, Blue = 1 }

public sealed class PlayerState : Component
{
	[Sync( SyncFlags.FromHost )] public Team Team { get; private set; } = Team.None;

	// Neutre (Core) : chaque mode peut choisir ses valeurs.
	[Sync( SyncFlags.FromHost )] public int MaxHealth { get; private set; } = 100;
	[Sync( SyncFlags.FromHost )] public int Health { get; private set; } = 100;

	[Sync( SyncFlags.FromHost )] public int Kills { get; private set; }

	private CameraComponent _camera;

	public float HealthFraction => MaxHealth <= 0 ? 0f : (float)Health / MaxHealth;
	public bool IsAlive => Health > 0;

	protected override void OnStart()
	{
		if ( !IsProxy )
			GameObject.Tags.Add( "localplayer" );

		_camera = Components.Get<CameraComponent>( FindMode.EverythingInSelfAndDescendants );
		if ( _camera != null )
			_camera.Enabled = !IsProxy;
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		if ( LootController.IsUiLockedLocal ) return;

		// IMPORTANT: pas de règles de vie ici (tests enlevés).
	}

	// --- API Host neutre ---

	public void SetTeamHost( Team t )
	{
		if ( !Networking.IsHost ) return;
		Team = t;
	}

	public void SetMaxHealthHost( int value )
	{
		if ( !Networking.IsHost ) return;
		MaxHealth = Math.Max( 0, value );
		Health = Math.Min( Health, MaxHealth );
	}

	public void SetHealthHost( int value )
	{
		if ( !Networking.IsHost ) return;
		Health = Math.Clamp( value, 0, MaxHealth );
	}

	public void AddHealthHost( int delta )
	{
		if ( !Networking.IsHost ) return;
		SetHealthHost( Health + delta );
	}

	// --- TP (neutre) ---
	public void TeleportHost( Vector3 pos, Rotation rot )
	{
		if ( !Networking.IsHost ) return;

		GameObject.Transform.World = new Transform( pos, rot );
		Network.ClearInterpolation();
		TeleportOwner( pos, rot );
	}

	[Rpc.Owner]
	private void TeleportOwner( Vector3 pos, Rotation rot )
	{
		GameObject.Transform.World = new Transform( pos, rot );
		Network.ClearInterpolation();
	}
}
