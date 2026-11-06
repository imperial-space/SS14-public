using Content.Shared.Actions;
using Content.Shared.Construction;
using Content.Shared.DoAfter;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Voidwalker;

/// <summary>/datum/brain_trauma/voided: космическая окраска тела.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ImperialVoidedComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color SpaceColor = Color.White;

    /// <summary>/datum/brain_trauma/voided/stable — от космического черепа.</summary>
    [DataField, AutoNetworkedField]
    public bool Stable;

    [DataField]
    public TimeSpan TickDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan UpdateTime = TimeSpan.FromSeconds(2);

    [DataField]
    public float UpdateRange = 0.9f;

    [DataField]
    public float UpdateChance = 0.02f;

    [DataField]
    public float DamagedChance = 0.3f;
}
