using Sandbox;
using Sandbox.UI;

namespace Astrofront;

public sealed class VitalsRootPanel : PanelComponent
{
    public Panel Root { get; private set; }

    protected override void OnTreeFirstBuilt()
    {
        if ( Panel == null ) 
        {
            Log.Warning($"{nameof(VitalsRootPanel)}: Panel is null.");
            return;
        }

        if ( Root != null )
        {
            Root.Delete( true );
            Root = null;
        }

        Root = new VitalsBar();

        Panel.AddClass( "fullscreen" );
        Panel.AddChild( Root );
    }

    protected override void OnDisabled()
    {
        Root?.Delete( true );
        Root = null;
        base.OnDisabled();
    }

    protected override void OnDestroy()
    {
        Root?.Delete( true );
        Root = null;
        base.OnDestroy();
    }
}
