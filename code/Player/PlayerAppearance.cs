using Sandbox;
using System.Threading.Tasks;

namespace Astrofront;

/// Synchronise et applique la tenue citizen (wardrobe) d'un joueur.
/// À ajouter sur le prefab joueur. Renseigne "Body" avec le SkinnedModelRenderer du mesh.
public sealed class PlayerAppearance : Component
{
    [Property, Title("Body Renderer")]
    public SkinnedModelRenderer Body { get; set; }

    // Tenue sérialisée (JSON). Fixée par le HOST puis répliquée à tout le monde.
    [Sync( SyncFlags.FromHost )]
    private string WardrobeData { get; set; }

    // Cache local pour éviter de ré-appliquer en boucle
    private string _applied;

    protected override void OnStart()
    {
        // Récupère le renderer si pas assigné + modèle par défaut si vide
        Body ??= Components.Get<SkinnedModelRenderer>( FindMode.EverythingInSelfAndDescendants );
        if ( Body != null && Body.Model == null )
            Body.Model = Model.Load( "models/citizen/citizen.vmdl" );

        // Si on est le propriétaire local, envoie notre wardrobe au host
        if ( !IsProxy )
            TrySendLocalWardrobeToHost();

        // Si on a déjà reçu WardrobeData (client tardif), applique-la une fois
        if ( !string.IsNullOrEmpty( WardrobeData ) && Body != null )
        {
            ApplyWardrobe( WardrobeData );
            _applied = WardrobeData;
        }

        // Fallback: si on est HOST et qu'on n'a rien reçu au début, on (re)demande au propriétaire
        if ( Networking.IsHost )
            _ = HostRequestOwnerWardrobeSoon();
    }

    protected override void OnUpdate()
    {
        // Applique seulement quand la valeur répliquée change
        if ( Body != null && !string.IsNullOrEmpty( WardrobeData ) && _applied != WardrobeData )
        {
            ApplyWardrobe( WardrobeData );
            _applied = WardrobeData;
        }
    }

    // ----- OWNER -> HOST -----

    /// Côté propriétaire local : récupère la tenue Steam locale et l’envoie au host.
    private void TrySendLocalWardrobeToHost()
    {
        var container = ClothingContainer.CreateFromLocalUser(); // API actuelle
        if ( container == null )
        {
            Log.Warning("[PlayerAppearance] Aucun wardrobe local (éditeur sans Steam ?).");
            return;
        }

        var data = container.Serialize();
        if ( string.IsNullOrEmpty( data ) )
        {
            Log.Warning("[PlayerAppearance] Wardrobe local vide.");
            return;
        }

        SubmitWardrobeHost( data ); // fonctionne même si l'owner est aussi le host
    }

    /// Reçoit la tenue transmise par l’owner – s’exécute sur le HOST.
    [Rpc.Host]
    public void SubmitWardrobeHost( string data )
    {
        if ( string.IsNullOrEmpty( data ) )
            return;

        WardrobeData = data;   // répliqué à tous
        _applied = null;       // force une (ré)application immédiate côté host

        if ( Body != null )
            ApplyWardrobe( WardrobeData );
    }

    // ----- HOST -> OWNER -----

    /// Demande (depuis le HOST) au propriétaire de renvoyer sa tenue.
    [Rpc.Owner]
    private void RequestWardrobeOwner()
    {
        TrySendLocalWardrobeToHost();
    }

    // Petit délai avant de demander (la connexion peut ne pas être prête tout de suite)
    private async Task HostRequestOwnerWardrobeSoon()
    {
        await Task.DelayRealtimeSeconds( 0.25f );

        if ( string.IsNullOrEmpty( WardrobeData ) )
        {
            // Si rien reçu après un court délai, on redemande gentiment
            RequestWardrobeOwner();
        }
    }

    // ----- Utils -----

    /// Applique une tenue sérialisée (JSON) sur le SkinnedModelRenderer.
    private void ApplyWardrobe( string data )
    {
        if ( Body == null || string.IsNullOrEmpty( data ) )
            return;

        var container = ClothingContainer.CreateFromJson( data ); // recrée depuis JSON
        container?.Apply( Body );
    }
}
