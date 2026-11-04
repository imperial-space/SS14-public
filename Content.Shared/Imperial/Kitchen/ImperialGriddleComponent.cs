using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>/obj/machinery/griddle (и /obj/machinery/grill): то, что лежит сверху, жарится.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialGriddleComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool On;

    [DataField]
    public int MaxItems = 8;

    /// <summary>Гриль на угле: работает без питания, пока есть топливо.</summary>
    [DataField]
    public bool NeedsPower = true;

    [DataField]
    public float Fuel;

    [DataField]
    public SoundSpecifier ToggleSound = new SoundPathSpecifier("/Audio/Imperial/Machines/click.ogg");
}
