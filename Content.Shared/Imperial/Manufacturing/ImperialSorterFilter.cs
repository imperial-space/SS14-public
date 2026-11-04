using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Manufacturing;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class ImperialSorterFilter
{
    [DataField]
    public ImperialSorterFilterType Type;

    /// <summary>dir_target: куда толкать подходящий предмет.</summary>
    [DataField]
    public Direction Dir = Direction.North;

    /// <summary>value: текст или прототип.</summary>
    [DataField]
    public string Value = string.Empty;

    /// <summary>Имя прототипа для «конкретного предмета».</summary>
    [DataField]
    public string ValueName = string.Empty;

    [DataField]
    public bool Inverted;

    /// <summary>currently_listening: ждёт удара предметом по сортировщику.</summary>
    public bool Listening;

    public int Id;
}
