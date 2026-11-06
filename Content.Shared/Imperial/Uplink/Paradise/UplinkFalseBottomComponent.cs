using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Paradise;

/// <summary>Кейс с ложным дном (Paradise, /obj/item/storage/briefcase/false_bottomed).</summary>
[RegisterComponent]
public sealed partial class UplinkFalseBottomComponent : Component
{
    [DataField]
    public string Container = "false_bottom";

    [DataField]
    public TimeSpan PryTime = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public bool Open;

    [ViewVariables]
    public bool Busy;
}
