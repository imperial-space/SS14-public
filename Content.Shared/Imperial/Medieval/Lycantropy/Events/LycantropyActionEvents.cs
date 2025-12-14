using Content.Shared.Actions;
using Robust.Shared.Audio;

namespace Content.Shared.Imperial.Medieval.Lycantropy;

public sealed partial class SelectWerewolfFormActionEvent : InstantActionEvent;

public sealed partial class OpenLycantropyMenuActionEvent : InstantActionEvent;

public sealed partial class WerewolfHowlActionEvent : InstantActionEvent;

public sealed partial class PolymorphWerewolfActionEvent : InstantActionEvent;

public sealed partial class ToggleLycantropyInfectActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

public sealed partial class WerewolfHealAlliesActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

public sealed partial class WerewolfRegenActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

public sealed partial class WerewolfStatusEffectActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public string Key = default!;

    [DataField]
    public string Component = String.Empty;

    [DataField]
    public float Time = 2.0f;

    [DataField]
    public bool Refresh = true;

    [DataField]
    public SoundSpecifier? Sound;

    [DataField]
    public bool Global = false;
}

public sealed partial class WerewolfTearingActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

public sealed partial class WerewolfShadowDashActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}

public sealed partial class WerewolfBloodFeelActionEvent : InstantActionEvent
{
    [DataField]
    public SoundSpecifier? Sound;
}
