using Sandbox;
using System.Threading.Tasks;

namespace Astrofront;

/// <summary>
/// Synchronise et applique la tenue citizen (wardrobe) d'un joueur.
/// Piloté par les "Rules" du mini-jeu via SetWardrobeEnabled / ClearClothing.
/// À ajouter sur le prefab joueur. Renseigne "Body" avec le SkinnedModelRenderer du mesh.
/// </summary>
public sealed class PlayerAppearance : Component
{
	[Property, Title( "Body Renderer" )]
	public SkinnedModelRenderer Body { get; set; }

	// Tenue sérialisée (JSON). Fixée par le HOST puis répliquée à tout le monde.
	[Sync( SyncFlags.FromHost )]
	private string WardrobeData { get; set; }

	// Cache local pour éviter de ré-appliquer en boucle
	private string _applied;

	// Flag local : est-ce que ce mode utilise le wardrobe Steam ?
	// (décidé par les Rules du mini-jeu)
	private bool _wardrobeEnabled = true;

	protected override void OnStart()
	{
		Body ??= Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );

		// Modèle citizen par défaut si vide (tu peux changer vers citizen_human.vmdl dans tes rules si besoin)
		if ( Body != null && Body.Model == null )
			Body.Model = Model.Load( "models/citizen/citizen.vmdl" );

		// Si on est le propriétaire local ET que wardrobe activé => envoie au host
		if ( !IsProxy && _wardrobeEnabled )
			TrySendLocalWardrobeToHost();

		// Si on a déjà reçu WardrobeData, applique-la (si wardrobe activé)
		if ( _wardrobeEnabled && !string.IsNullOrEmpty( WardrobeData ) && Body != null )
		{
			ApplyWardrobe( WardrobeData );
			_applied = WardrobeData;
		}

		// Fallback host : redemande si wardrobe activé et rien reçu
		if ( Networking.IsHost )
			_ = HostRequestOwnerWardrobeSoon();
	}

	protected override void OnUpdate()
	{
		// Applique seulement quand la valeur répliquée change, et uniquement si wardrobe activé
		if ( _wardrobeEnabled && Body != null && !string.IsNullOrEmpty( WardrobeData ) && _applied != WardrobeData )
		{
			ApplyWardrobe( WardrobeData );
			_applied = WardrobeData;
		}
	}

	// =========================
	// API appelée par les Rules
	// =========================

	/// <summary>
	/// Active/désactive l'utilisation du wardrobe Steam.
	/// - true  => le joueur porte sa tenue Steam (si dispo)
	/// - false => on enlève tous les vêtements (citizen "nu")
	/// </summary>
	public void SetWardrobeEnabled( bool enabled )
	{
		_wardrobeEnabled = enabled;

		if ( !_wardrobeEnabled )
		{
			// Retire les vêtements immédiatement
			ClearClothing();
			return;
		}

		// Si on vient d'activer le wardrobe : owner envoie au host, et on applique si déjà reçu
		if ( !IsProxy )
			TrySendLocalWardrobeToHost();

		if ( !string.IsNullOrEmpty( WardrobeData ) && Body != null )
		{
			ApplyWardrobe( WardrobeData );
			_applied = WardrobeData;
		}
	}

	/// <summary>
	/// Retire tous les vêtements (citizen sans apparence Steam).
	/// </summary>
	public void ClearClothing()
	{
		if ( Body == null )
			return;

		// Applique un container vide = enlève tous les vêtements
		var empty = new ClothingContainer();
		empty.Apply( Body );

		_applied = null;
	}

	/// <summary>
	/// Change le modèle du corps (ex: citizen_human) sans toucher au wardrobe.
	/// Utile si un mode RP veut citizen_human.vmdl.
	/// </summary>
	public void SetBodyModel( string modelPath )
	{
		if ( Body == null || string.IsNullOrEmpty( modelPath ) )
			return;

		Body.Model = Model.Load( modelPath );

		// Si wardrobe désactivé, on s'assure de rester "sans vêtements"
		if ( !_wardrobeEnabled )
			ClearClothing();
		else
			_applied = null; // force une ré-application wardrobe à la prochaine update si WardrobeData est là
	}

	// =========================
	// OWNER -> HOST
	// =========================

	private void TrySendLocalWardrobeToHost()
	{
		if ( !_wardrobeEnabled )
			return;

		var container = ClothingContainer.CreateFromLocalUser();
		if ( container == null )
		{
			Log.Warning( "[PlayerAppearance] Aucun wardrobe local (éditeur sans Steam ?)." );
			return;
		}

		var data = container.Serialize();
		if ( string.IsNullOrEmpty( data ) )
		{
			Log.Warning( "[PlayerAppearance] Wardrobe local vide." );
			return;
		}

		SubmitWardrobeHost( data );
	}

	[Rpc.Host]
	public void SubmitWardrobeHost( string data )
	{
		if ( string.IsNullOrEmpty( data ) )
			return;

		WardrobeData = data;
		_applied = null;

		// Applique uniquement si wardrobe activé
		if ( _wardrobeEnabled && Body != null )
			ApplyWardrobe( WardrobeData );
	}

	// =========================
	// HOST -> OWNER
	// ========================= 

	[Rpc.Owner]
	private void RequestWardrobeOwner()
	{
		TrySendLocalWardrobeToHost();
	}

	private async Task HostRequestOwnerWardrobeSoon()
	{
		await Task.DelayRealtimeSeconds( 0.25f );

		if ( !_wardrobeEnabled )
			return;

		if ( string.IsNullOrEmpty( WardrobeData ) )
			RequestWardrobeOwner();
	}

	// =========================
	// Utils
	// =========================

	private void ApplyWardrobe( string data )
	{
		if ( Body == null || string.IsNullOrEmpty( data ) )
			return;

		var container = ClothingContainer.CreateFromJson( data );
		container?.Apply( Body );
	}
}
