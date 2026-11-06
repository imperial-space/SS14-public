using Content.Shared.Imperial.MartialArts;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MartialArts;

public enum MartialManualRecoil : byte
{
    None,

    /// <summary>granter/martial/cqc/recoil(): учебник взрывается в руках.</summary>
    Explode,
}
