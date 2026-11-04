using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Фазовый сдвиг призрака (ethereal_jaunt/shift).</summary>
[RegisterComponent]
public sealed partial class CultPhasedComponent : Component
{
    [ViewVariables] public TimeSpan End;
    [ViewVariables] public Robust.Shared.Map.EntityCoordinates? LastSafe;
    [ViewVariables] public Dictionary<string, (int Mask, int Layer)> Masks = new();
}
