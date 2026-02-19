using Sandbox;

namespace Astrofront;

[AssetType( Name = "Item Definition", Extension = "itemdef", Category = "gameplay" )]
public partial class ItemDefinition : GameResource
{
	[Property] public string Id { get; set; } = "core.test.item";
	[Property] public string DisplayName { get; set; } = "Test Item";
	[Property] public string UiClass { get; set; } = "type-generic";

	[Property, ResourceType( "png,jpg,texture" )]
	public string Icon { get; set; }

	[Property] public int MaxStack { get; set; } = 9999;
	[Property] public int SpaceCost { get; set; } = 1;

	// ✅ 1 prefab par item (comme SWB)
	[Property, Group("Presentation")]
	public GameObject ItemPrefab { get; set; }
}
