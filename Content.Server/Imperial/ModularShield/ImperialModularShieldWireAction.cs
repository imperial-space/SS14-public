using Content.Server.Wires;
using Content.Shared.Imperial.ModularShield;
using Content.Shared.Wires;

namespace Content.Server.Imperial.ModularShield;

/// <summary>WIRE_HACK генератора модульного щита: импульс переключает щит.</summary>
public sealed partial class ImperialModularShieldWireAction : ComponentWireAction<ImperialModularShieldGeneratorComponent>
{
    public override Color Color { get; set; } = Color.Cyan;
    public override string Name { get; set; } = "wire-name-imperial-modular-shield";

    public override object StatusKey => ImperialModularShieldWireStatus.Key;

    public override StatusLightState? GetLightState(Wire wire, ImperialModularShieldGeneratorComponent comp)
    {
        return comp.Active ? StatusLightState.On : StatusLightState.Off;
    }

    public override bool Cut(EntityUid user, Wire wire, ImperialModularShieldGeneratorComponent comp)
    {
        return true;
    }

    public override bool Mend(EntityUid user, Wire wire, ImperialModularShieldGeneratorComponent comp)
    {
        return true;
    }

    public override void Pulse(EntityUid user, Wire wire, ImperialModularShieldGeneratorComponent comp)
    {
        EntityManager.System<ImperialModularShieldSystem>().ToggleShields((wire.Owner, comp));
    }
}
