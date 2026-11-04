using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>Нажатие кнопки способности без цели.</summary>
public sealed partial class ImperialChangelingPowerActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public ProtoId<ImperialChangelingPowerPrototype> Power;
}
