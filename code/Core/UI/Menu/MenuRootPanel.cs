// /code/UI/MenuRootPanel.cs
using Sandbox;
using Sandbox.UI;

namespace Astrofront;

/// Monte /ui/MainMenu.razor sur l'écran (ScreenPanel requis sur le même GO).
public sealed class MenuRootPanel : PanelComponent
{
    private Panel _menu;

    protected override void OnTreeFirstBuilt()
    {
        base.OnTreeFirstBuilt();

        // Instancie la classe générée par /ui/MainMenu.razor
        _menu = new MainMenu();
        _menu.Parent = Panel;       // Panel = racine du ScreenPanel
        _menu.AddClass( "fullscreen" );

        Mouse.Visibility = MouseVisibility.Visible; // curseur visible dans le menu
    }

    protected override void OnDestroy()
    {
        _menu?.Delete( true );
        _menu = null;
        base.OnDestroy();
    }
}
