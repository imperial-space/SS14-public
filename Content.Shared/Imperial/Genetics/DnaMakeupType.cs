using Content.Shared.Imperial.Genetics.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Genetics;

/// <summary>Часть генетического облика: ферменты (имя и ДНК), идентичность (внешность), особенности, всё сразу.</summary>
[Serializable, NetSerializable]
public enum DnaMakeupType : byte
{
    Enzymes,
    Identity,
    Features,
    Mixed,
}
