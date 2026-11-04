using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.TerrorSpider;

[Serializable, NetSerializable]
public sealed class TerrorSpiderChoiceBoundUserInterfaceState : BoundUserInterfaceState
{
    public TerrorSpiderChoiceMode Mode { get; }
    public string Title { get; }
    public TerrorSpiderChoiceOption[] Options { get; }

    /// <summary>«How many in the batch?»; пусто — без выбора количества.</summary>
    public int[] Counts { get; }

    /// <summary>«%chance to be stillborn?»; пусто — без выбора.</summary>
    public int[] Percents { get; }

    public TerrorSpiderChoiceBoundUserInterfaceState(TerrorSpiderChoiceMode mode, string title,
        TerrorSpiderChoiceOption[] options, int[] counts, int[] percents)
    {
        Mode = mode;
        Title = title;
        Options = options;
        Counts = counts;
        Percents = percents;
    }
}
