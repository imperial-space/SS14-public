using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Cult;

[Serializable, NetSerializable]
public sealed class CultTextState(string title, string prompt, int maxLength) : BoundUserInterfaceState
{
    public readonly string Title = title;
    public readonly string Prompt = prompt;
    public readonly int MaxLength = maxLength;
}
