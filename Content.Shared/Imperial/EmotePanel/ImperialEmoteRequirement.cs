using Content.Shared.Alert;
using Content.Shared.Chat.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.EmotePanel;

/// <summary>Особые проверки can_run_emote.</summary>
public enum ImperialEmoteRequirement : byte
{
    /// <summary>can_speak(allow_mimes = TRUE).</summary>
    CanSpeak,

    /// <summary>usable_hands >= 2.</summary>
    TwoHands,

    /// <summary>Обычные очки или солнцезащитные на глазах.</summary>
    Glasses,

    /// <summary>Хвост, которым можно вилять.</summary>
    Tail,

    /// <summary>Крылья.</summary>
    Wings,
}
