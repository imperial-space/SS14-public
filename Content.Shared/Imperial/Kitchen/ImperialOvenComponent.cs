using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>/obj/machinery/oven: противень внутри, закрытая и запитанная духовка печёт.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialOvenComponent : Component
{
    public const string TraySlot = "oven_tray";

    [DataField, AutoNetworkedField]
    public bool Open;

    [DataField]
    public EntProtoId? StartingTray = "ImperialOvenTray";

    [DataField]
    public SoundSpecifier OpenSound = new SoundPathSpecifier("/Audio/Imperial/Kitchen/oven_open.ogg");

    [DataField]
    public SoundSpecifier CloseSound = new SoundPathSpecifier("/Audio/Imperial/Kitchen/oven_close.ogg");

    [ViewVariables]
    public TimeSpan NextSmell;
}
