using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>spell_requirements: флаги SPELL_REQUIRES_*.</summary>
[Flags, Serializable, NetSerializable]
public enum ImperialSpellRequirements : ushort
{
    None = 0,
    WizardGarb = 1 << 0,
    NoAntimagic = 1 << 1,
    Station = 1 << 2,
    Human = 1 << 3,
    Mind = 1 << 4,
    MimeVow = 1 << 5,
    CastableAsBrain = 1 << 6,
    CastableWithoutInvocation = 1 << 7,
}
