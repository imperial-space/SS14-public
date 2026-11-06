using Content.Server.Chat.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Xenomorph;

/// <summary>
/// Мидраунд «Alien Infestation» (dynamic_ruleset/midround/from_ghosts/xenomorph): 1 личинка (с шансом 50 % — вторая)
/// в вентиляции как гост-роль, объявление о неопознанных формах жизни через 375–600 секунд.
/// </summary>
[RegisterComponent]
public sealed partial class XenomorphInfestationRuleComponent : Component
{
    [DataField]
    public EntProtoId Spawner = "ImperialXenoLarvaSpawner";

    [DataField]
    public TimeSpan MinAnnounceDelay = TimeSpan.FromSeconds(375);

    [DataField]
    public TimeSpan MaxAnnounceDelay = TimeSpan.FromSeconds(600);

    [DataField]
    public SoundSpecifier AnnouncementSound = new SoundPathSpecifier("/Audio/Imperial/Xenomorph/aliens.ogg");

    [ViewVariables]
    public TimeSpan? AnnounceAt;

    [DataField]
    public float StartedChance = 0.5f;
}
