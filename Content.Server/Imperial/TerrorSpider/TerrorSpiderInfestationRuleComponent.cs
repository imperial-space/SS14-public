using Robust.Shared.Random;
using Content.Server.Chat.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>
/// events/spider_terror.dm: пауки Ужаса из вентиляции, объявление через 240–270 секунд.
/// </summary>
[RegisterComponent]
public sealed partial class TerrorSpiderInfestationRuleComponent : Component
{
    /// <summary>infestation_type; Random — по населённости, как в wrappedstart().</summary>
    [DataField]
    public TerrorSpiderInfestationType Infestation = TerrorSpiderInfestationType.Random;

    [DataField]
    public TimeSpan MinAnnounceDelay = TimeSpan.FromSeconds(240);

    [DataField]
    public TimeSpan MaxAnnounceDelay = TimeSpan.FromSeconds(270);

    [DataField]
    public SoundSpecifier AnnouncementSound = new SoundPathSpecifier("/Audio/Imperial/TerrorSpider/outbreak_terror.ogg");

    [ViewVariables]
    public TimeSpan? AnnounceAt;
}
