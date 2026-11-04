using Content.Shared.Cargo.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Speech;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Vending;

/// <summary>mailsorter TG: сортирует посылки по отделам получателей.</summary>
[RegisterComponent]
public sealed partial class ImperialMailSorterComponent : Component
{
    [DataField]
    public string Container = "mail_sorter";

    [DataField]
    public int Capacity = 100;

    [DataField]
    public TimeSpan SortDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier SortSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    [DataField]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");

    [DataField]
    public SoundSpecifier SuccessSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");

    [ViewVariables]
    public bool Sorting;
}
