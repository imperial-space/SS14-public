using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>Одна итерация поглощения (attempt_absorb, 3 × 15 с).</summary>
[Serializable, NetSerializable]
public sealed partial class ImperialChangelingAbsorbDoAfterEvent : DoAfterEvent
{
    [DataField]
    public int Stage;

    public ImperialChangelingAbsorbDoAfterEvent(int stage)
    {
        Stage = stage;
    }

    public override DoAfterEvent Clone() => new ImperialChangelingAbsorbDoAfterEvent(Stage);
}
