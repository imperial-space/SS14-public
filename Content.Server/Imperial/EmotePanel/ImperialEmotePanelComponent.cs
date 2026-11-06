using Content.Server.Chat.Systems;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Imperial.EmotePanel;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.EmotePanel;

/// <summary>Действие панели эмоций и состояние эмоций существа.</summary>
[RegisterComponent]
public sealed partial class ImperialEmotePanelComponent : Component
{
    [ViewVariables]
    public EntityUid? Action;
    [ViewVariables]
    public TimeSpan NextCustomEmote;

    /// <summary>emotes_used: когда каждая эмоция снова доступна.</summary>
    [ViewVariables]
    public Dictionary<string, TimeSpan> Cooldowns = new();

    /// <summary>MANUAL_GENERAL_EMOTE_AUDIO_COOLDOWN.</summary>
    [ViewVariables]
    public TimeSpan NextSound;

    /// <summary>MANUAL_SPECIFIC_EMOTE_AUDIO_COOLDOWN по эмоциям.</summary>
    [ViewVariables]
    public Dictionary<string, TimeSpan> NextSpecificSound = new();

    /// <summary>COOLDOWN_YAWN_PROPAGATION.</summary>
    [ViewVariables]
    public TimeSpan YawnImmuneUntil;

    /// <summary>client.recent_examines: кого и когда осматривало существо.</summary>
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> RecentExamines = new();
}
