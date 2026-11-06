using Content.Shared.Imperial.MartialArts;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MartialArts;

/// <summary>implant/kaza_ruk, implant/cqc: обучает при вживлении, кнопкой — вкл/выкл.</summary>
[RegisterComponent]
public sealed partial class MartialArtImplantComponent : Component
{
    [DataField(required: true)]
    public ProtoId<MartialArtPrototype> Art;
}
