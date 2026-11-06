using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Manufacturing;

[Serializable, NetSerializable]
public sealed class ImperialSorterFilterData
{
    public int Id;
    public string Name = string.Empty;
    public bool Inverted;
    public Direction Dir;

    /// <summary>Текст фильтра name_contains; null — фильтр не текстовый.</summary>
    public string? TextValue;
}
