using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>Hallucinations: кровавые искры над целью (видят только культисты).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CultHorrorMarkComponent : Component
{
    [ViewVariables] public TimeSpan End;
}
