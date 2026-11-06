using Content.Shared.Alert;
using Content.Shared.Chat.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.EmotePanel;

/// <summary>Что эмоция делает помимо текста и звука (run_emote TG).</summary>
public enum ImperialEmoteEffect : byte
{
    None,
    Flip,
    Backflip,
    Spin,
    Jump,
    Taunt,
    Dance,
    Shiver,
    Sway,
    Tremble,
    Twitch,
    TwitchSlight,
    Tongue,
    Blush,
    Cry,
    Sigh,
    Glasses,
    Collapse,
    Faint,
    Surrender,
    Yawn,
    Wag,
    Point,
    AiDisplay,
}
