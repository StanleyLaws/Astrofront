using Sandbox;
using System;

namespace Astrofront;

public enum Team { None = -1, Red = 0, Blue = 1 }

/// <summary>
/// Données joueur synchronisées + API Host neutre.
/// IMPORTANT: aucune lecture d'input ici (les inputs sont gérés par des components dédiés).
/// </summary>
public sealed class PlayerState : Component
{
	[Sync( SyncFlags.FromHost )] public Team Team { get; private set; } = Team.None;

	// Health (neutre)
	[Sync( SyncFlags.FromHost )] public int MaxHealth { get; private set; } = 100;
	[Sync( SyncFlags.FromHost )] public int Health { get; private set; } = 100;

	// Energy (neutre)
	[Sync( SyncFlags.FromHost )] public int MaxEnergy { get; private set; } = 100;
	[Sync( SyncFlags.FromHost )] public int Energy { get; private set; } = 100;

	[Sync( SyncFlags.FromHost )] public int Kills { get; private set; }

	public float HealthFraction => MaxHealth <= 0 ? 0f : (float)Health / MaxHealth;
	public float EnergyFraction => MaxEnergy <= 0 ? 0f : (float)Energy / MaxEnergy;

	public bool IsAlive => Health > 0;

	protected override void OnStart()
	{
		// Sert à retrouver le joueur local facilement côté UI / rules bootstrap.
		if ( !IsProxy )
			GameObject.Tags.Add( "localplayer" );

		// --- TEST PIPELINE HELD ITEM (Host only) ---
		// IMPORTANT: InventoryComponent peut ne pas avoir encore fait son OnStart ici.
		// On décale d'1 frame pour éviter NullReference sur GetSlotsSnapshot().
		if ( Networking.IsHost )
		{
			GiveTestItemAfterInventoryInit();
		}

		async void GiveTestItemAfterInventoryInit()
		{
			// 1 frame suffit généralement pour que InventoryComponent.OnStart() ait initialisé ses arrays.
			await GameTask.Delay( 1 );

			if ( !this.IsValid() || !GameObject.IsValid() ) return;

			var inv = GameObject.Components.Get<InventoryComponent>( FindMode.EverythingInSelfAndDescendants );
			if ( inv == null ) return;

			var snap = inv.GetSlotsSnapshot();
			bool slot1Empty =
				snap == null || snap.Count <= 1 ||
				string.IsNullOrEmpty( snap[1].ItemId ) ||
				snap[1].Amount <= 0;

			if ( slot1Empty )
			{
				const string TestItemId = "core.test.item";
				inv.AddHost( TestItemId, 1 );
				Log.Info( $"[PlayerState] Gave test item '{TestItemId}' to inventory (host)." );
			}
		}
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

	// --- Energy API Host neutre ---

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
