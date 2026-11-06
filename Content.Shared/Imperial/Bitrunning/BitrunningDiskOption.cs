using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>/obj/item/disk/bitrunning: вариант программы — предметы и/или способности аватара.</summary>
[DataDefinition, Serializable]
public sealed partial class BitrunningDiskOption
{
    [DataField(required: true)]
    public LocId Name;

    /// <summary>Строками: часть предметов из других веток (Лаваленд, боевые искусства), без них вариант скрыт.</summary>
    [DataField]
    public List<string> Items = new();

    [DataField]
    public List<EntProtoId> Actions = new();
}
