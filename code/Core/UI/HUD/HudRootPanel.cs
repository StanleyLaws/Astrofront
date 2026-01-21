using Sandbox;
using Sandbox.UI;

namespace Astrofront;

public sealed class HudRootPanel : PanelComponent
{
    private Panel _hud;

    protected override void OnTreeFirstBuilt()
    {
        base.OnTreeFirstBuilt();

        _hud = new Hud();     // classe générée par /ui/Hud.razor
        _hud.Parent = Panel;  // nécessite un ScreenPanel sur le même GO
        _hud.AddClass( "fullscreen" );
    }

    protected override void OnDestroy()
    {
        _hud?.Delete( true );
        _hud = null;
        base.OnDestroy();
    }
}
