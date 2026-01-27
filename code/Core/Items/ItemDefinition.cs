using Sandbox;

namespace Astrofront;

[AssetType( Name = "Item Definition", Extension = "itemdef", Category = "gameplay" )]
public partial class ItemDefinition : GameResource
{
	/// Unique et stable. Ex: "core.test.item" ou "astrofront.resource.stellium"
	[Property] public string Id { get; set; } = "core.test.item";

	[Property] public string DisplayName { get; set; } = "Test Item";

	/// Classe CSS optionnelle pour réutiliser TON style existant (type-stellium, etc.)
	[Property] public string UiClass { get; set; } = "type-generic";

	/// Icone UI (optionnel)
	[Property, ResourceType("png,jpg,texture")] public string Icon { get; set; }

	/// Modèle monde (optionnel)
	[Property, ResourceType("vmdl")] public string WorldModel { get; set; }

	[Property] public int MaxStack { get; set; } = 9999;

	/// “Coût espace” si tu veux garder le système CapacitySpace
	[Property] public int SpaceCost { get; set; } = 1;
}
