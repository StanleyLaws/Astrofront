global using Sandbox;
global using System.Collections.Generic;
global using System.Linq;

namespace Astrofront;

public static class InputGate
{
    /// Input gameplay autorisé (mouvement, caméra, actions) ?
    public static bool CanGameplayInput => !UiModalController.IsUiLockedLocal;

    /// Input UI autorisé ? (ex: navigation inventaire)
    public static bool CanUiInput => true; 
}
