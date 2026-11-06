using Content.Shared.Alert;
using Content.Shared.Chat.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.EmotePanel;

/// <summary>Кто может выполнить эмоцию (mob_type_allowed_typecache TG).</summary>
public enum ImperialEmoteMob : byte
{
    /// <summary>/mob/living: любое живое существо, кроме ИИ.</summary>
    Living,

    /// <summary>/mob/living/carbon: гуманоиды, обезьяны, ксеноморфы.</summary>
    Carbon,

    /// <summary>/mob/living/carbon/human.</summary>
    Human,

    Monkey,
    Alien,

    /// <summary>TRAIT_SILICON_EMOTES_ALLOWED: киборги и ИИ.</summary>
    Silicon,

    /// <summary>/mob/living/silicon/ai.</summary>
    Ai,
}
