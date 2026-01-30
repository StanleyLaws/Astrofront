using Sandbox;
using System;

namespace Astrofront;

public enum Team { None = -1, Red = 0, Blue = 1 }

public sealed class PlayerState : Component
{
	[Sync( SyncFlags.FromHost )] public Team Team { get; private set; } = Team.None;

	// Health (neutre)
	[Sync( SyncFlags.FromHost )] public int MaxHealth { get; private set; } = 100;
	[Sync( SyncFlags.FromHost )] public int Health { get; private set; } = 100;

	// ✅ Energy (neutre)
	[Sync( SyncFlags.FromHost )] public int MaxEnergy { get; private set; } = 100;
	[Sync( SyncFlags.FromHost )] public int Energy { get; private set; } = 100;

	[Sync( SyncFlags.FromHost )] public int Kills { get; private set; }

	public float HealthFraction => MaxHealth <= 0 ? 0f : (float)Health / MaxHealth;
	public float EnergyFraction => MaxEnergy <= 0 ? 0f : (float)Energy / MaxEnergy;

	public bool IsAlive => Health > 0;

	protected override void OnStart()
	{
		if ( !IsProxy )
			GameObject.Tags.Add( "localplayer" );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		if ( UiModalController.IsUiLockedLocal ) return;

		// Test déjà existant
		if ( Input.Pressed( "Flashlight" ) )
		{
			DamageSelfTestHost( 0.05f );
		}

		// ✅ Test énergie (optionnel, juste pour voir la barre bouger)
		// Exemple : Sprint => -5 énergie
		if ( Input.Pressed( "Test" ) )
		{
			AddEnergyHost( -5 );
		}
	}

	[Rpc.Host]
	private void DamageSelfTestHost( float fraction )
	{
		var amount = (int)(MaxHealth * fraction);
		AddHealthHost( -amount );
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

	// ✅ Energy API Host neutre

	public void SetMaxEnergyHost( int value )
	{
		if ( !Networking.IsHost ) return;
		MaxEnergy = Math.Max( 0, value );
		Energy = Math.Min( Energy, MaxEnergy );
	}

	public void SetEnergyHost( int value )
	{
		if ( !Networking.IsHost ) return;
		Energy = Math.Clamp( value, 0, MaxEnergy );
	}

	public void AddEnergyHost( int delta )
	{
		if ( !Networking.IsHost ) return;
		SetEnergyHost( Energy + delta );
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
