using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.TerrorSpider;

[Serializable, NetSerializable]
public sealed class TerrorSpiderChoiceMessage : BoundUserInterfaceMessage
{
    public TerrorSpiderChoiceMode Mode { get; }
    public string Key { get; }
    public int Count { get; }
    public int Percent { get; }

    public TerrorSpiderChoiceMessage(TerrorSpiderChoiceMode mode, string key, int count, int percent)
    {
        Mode = mode;
        Key = key;
        Count = count;
        Percent = percent;
    }
}
