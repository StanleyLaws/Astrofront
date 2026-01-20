using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Network;

namespace Astrofront
{
  /// Log + handoff propre : un seul ENTER/EXIT par joueur,
  /// même si le joueur a plusieurs sous-colliders.
  public sealed class TriggerProbe : Component, Component.ITriggerListener
  {
    [Property, Title("Nom logique de la zone")]
    public string ZoneName { get; set; } = "PortalToGame";

    [Property, Title("Debounce ENTER (s)")]
    public float DebounceSeconds { get; set; } = 0.3f;

    // nb de colliders du même joueur actuellement dans la zone
    private readonly Dictionary<Guid, int> _overlapCount = new();
    // anti-doublon court sur ENTER (évite 0→1→0→1 très rapides)
    private readonly Dictionary<Guid, RealTimeSince> _lastEnterAt = new();

    protected override void OnStart()
    {
      var col = Components.Get<Collider>(FindMode.InSelf);
      if (col == null || !col.IsTrigger)
        Log.Warning($"[TriggerProbe:{ZoneName}] Collider manquant ou 'Is Trigger' non coché.");
      Log.Info($"[TriggerProbe:{ZoneName}] Ready. IsClient={Networking.IsClient}, IsActive={Networking.IsActive}");
    }

    public void OnTriggerEnter(Collider other)
    {
      var root = other?.GameObject?.Root;
      var id = root?.Id ?? Guid.Empty;
      if (id == Guid.Empty) return;

      // ++compteur pour ce joueur
      _overlapCount.TryGetValue(id, out var n);
      n++;
      _overlapCount[id] = n;

      // Ne réagir qu’au premier ENTER (0 -> 1)
      if (n == 1)
      {
        // Debounce court optionnel
        if (_lastEnterAt.TryGetValue(id, out var since) && since < DebounceSeconds)
          return;

        _lastEnterAt[id] = 0;
        Log.Info($"[TriggerProbe:{ZoneName}] ENTER (root='{root?.Name}')");

        if (Networking.IsClient)
          _ = Astrofront.GameInstanceConnector.ConnectToAvailableGame("AF_1x1_"); // adapte le préfixe si besoin
      }
    }

    public void OnTriggerExit(Collider other)
    {
      var root = other?.GameObject?.Root;
      var id = root?.Id ?? Guid.Empty;
      if (id == Guid.Empty) return;

      if (_overlapCount.TryGetValue(id, out var n))
      {
        n = Math.Max(0, n - 1);
        if (n == 0)
        {
          _overlapCount.Remove(id);
          Log.Info($"[TriggerProbe:{ZoneName}] EXIT  (root='{root?.Name}')");
        }
        else
        {
          _overlapCount[id] = n; // reste encore des sous-colliders dedans → pas d’EXIT “global”
        }
      }
    }
  }
}
