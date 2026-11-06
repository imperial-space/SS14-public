using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Магнитофон (taperecorder): записывает речь вокруг и воспроизводит её.</summary>
[RegisterComponent]
public sealed partial class ImperialTapeRecorderComponent : Component
{
    [DataField]
    public int MaxLines = 30;

    [ViewVariables]
    public bool Recording;

    /// <summary>Записанные реплики; в прототипе — готовая плёнка (tape/frozen).</summary>
    [DataField]
    public List<string> Lines = new();

    [ViewVariables]
    public int PlaybackIndex = -1;

    [ViewVariables]
    public TimeSpan NextLine;

    [DataField]
    public TimeSpan LineDelay = TimeSpan.FromSeconds(2);
}
