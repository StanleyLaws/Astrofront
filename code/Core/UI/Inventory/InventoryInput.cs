using Sandbox;

namespace Astrofront;

/// <summary>
/// Gère uniquement la sélection de slot (HUD rapide) + drop 1 item.
/// - Aucun UI
/// - Juste des intentions d’input
/// </summary>
public sealed class InventoryInput : Component
{
	private InventoryComponent _inv;

	protected override void OnStart()
	{
		_inv = GameObject.Components.Get<InventoryComponent>(
			FindMode.InSelf | FindMode.InChildren
		);
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		if ( UiModalController.IsUiLockedLocal ) return;

		if ( _inv == null )
		{
			_inv = GameObject.Components.Get<InventoryComponent>(
				FindMode.InSelf | FindMode.InChildren
			);
			if ( _inv == null ) return;
		}

		// ===== Drop (1 item) =====
		if ( Input.Pressed( InputActions.Drop ) )
		{
			// Hands interdit
			if ( _inv.SelectedIndex != 0 )
			{
				_inv.RequestDropHost( _inv.SelectedIndex, 1 );
			}
		}

		// ===== Sélection directe =====
		if ( Input.Pressed( InputActions.Slot1 ) ) _inv.SetSelected( 0 ); // HANDS
		if ( Input.Pressed( InputActions.Slot2 ) ) _inv.SetSelected( 1 );
		if ( Input.Pressed( InputActions.Slot3 ) ) _inv.SetSelected( 2 );
		if ( Input.Pressed( InputActions.Slot4 ) ) _inv.SetSelected( 3 );
		if ( Input.Pressed( InputActions.Slot5 ) ) _inv.SetSelected( 4 );

		// ===== Molette =====
		var wheel = Input.MouseWheel;
		if ( wheel.y > 0f ) Prev();
		else if ( wheel.y < 0f ) Next();
	}

	private void Next()
	{
		int next = (_inv.SelectedIndex + 1) % _inv.SlotCount;
		_inv.SetSelected( next );
	}

	private void Prev()
	{
		int prev = _inv.SelectedIndex - 1;
		if ( prev < 0 ) prev = _inv.SlotCount - 1;
		_inv.SetSelected( prev );
	}
}
