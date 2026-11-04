using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Действует «Мутация» мага: зелёный цвет халка, гигантизм, лазерные глаза.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialWizardMutatedComponent : Component
{
    [ViewVariables]
    public TimeSpan End;

    [ViewVariables]
    public EntityUid? LaserAction;
}
