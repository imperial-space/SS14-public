using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.TerrorSpider;

[Serializable, NetSerializable]
public sealed class TerrorSpiderChoiceOption
{
    public string Key { get; }
    public string Label { get; }

    public TerrorSpiderChoiceOption(string key, string label)
    {
        Key = key;
        Label = label;
    }
}
