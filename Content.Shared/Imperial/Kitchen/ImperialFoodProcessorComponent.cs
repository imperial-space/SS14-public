using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>/obj/machinery/processor.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialFoodProcessorComponent : Component
{
    public const string Container = "processor";

    [DataField, AutoNetworkedField]
    public bool Processing;

    /// <summary>rating_speed деталей.</summary>
    [DataField]
    public float Speed = 1f;

    /// <summary>rating_amount деталей.</summary>
    [DataField]
    public int Amount = 1;

    [ViewVariables]
    public TimeSpan EndTime;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Machines/blender.ogg");
}
