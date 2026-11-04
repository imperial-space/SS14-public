using Content.Shared.Atmos;
using Content.Shared.Imperial.Hypertorus;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Hypertorus;

/// <summary>Коробка детали (obj/item/hfr_box): ставится 3×3, ядро разворачивает всё мультитулом.</summary>
[RegisterComponent]
public sealed partial class HypertorusBoxComponent : Component
{
    [DataField(required: true)]
    public HypertorusBoxType BoxType;

    [DataField(required: true)]
    public EntProtoId Part;
}
