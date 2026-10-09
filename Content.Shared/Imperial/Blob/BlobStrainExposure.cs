using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Blob;

/// <summary>
/// Эффект реагента блоба при касании (облако спор): срабатывает как expose_mob() штамма.
/// </summary>
public sealed partial class BlobStrainExposure : EntityEffectBase<BlobStrainExposure>
{
    [DataField(required: true)]
    public ProtoId<BlobStrainPrototype> Strain;
}
