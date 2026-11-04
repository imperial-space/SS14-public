using System.Linq;
using System.Numerics;
using Content.Server.AlertLevel;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Mining;
using Content.Server.Station.Systems;
using Content.Shared.Chat;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Emag.Systems;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.MeteorShield;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.MeteorShield;

/// <summary>Тёмно-материальный метеор: отражает лучи щитов и роняет сингулярность.</summary>
[RegisterComponent]
public sealed partial class DarkMatteorComponent : Component
{
    /// <summary>meteorsound: при каждом ударе и при финальном взрыве (meteor_effect) для всех на карте.</summary>
    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Imperial/MeteorShield/curse1.ogg");

    [DataField]
    public SoundSpecifier AnnouncementSound = new SoundPathSpecifier("/Audio/Imperial/MeteorShield/airraid.ogg");

    [ViewVariables] public TimeSpan NextImpactSound;
    [ViewVariables] public EntityUid? Station;
    [ViewVariables] public string? PreviousLevel;
    [ViewVariables] public bool Hit;
}
