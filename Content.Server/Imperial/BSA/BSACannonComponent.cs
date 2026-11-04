using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Emag.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Warps;
using Content.Shared.Imperial.BSA;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Tools.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.BSA;

/// <summary>Собранная пушка (obj/machinery/bsa/full).</summary>
[RegisterComponent]
public sealed partial class BSACannonComponent : Component
{
    /// <summary>Ствол смотрит на запад (иначе на восток).</summary>
    [DataField]
    public bool West = true;

    /// <summary>ex_power: взрыв у цели 3/6/12.</summary>
    [DataField]
    public float ExPower = 3f;

    /// <summary>power_used_per_shot: в SS13 «хватает, чтобы убить стандартный APC».</summary>
    [DataField]
    public float PowerPerShot = 40000f;

    /// <summary>Перезарядка (BANDASTATION ADD: BSA cooldown check).</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(1);

    /// <summary>Как далеко идёт луч (в SS13 — до края карты).</summary>
    [DataField]
    public int BeamRange = 150;

    [DataField]
    public EntProtoId Top = "ImperialBSACannonTopWest";

    [DataField]
    public EntProtoId Beam = "ImperialBSABeam";

    [DataField]
    public EntProtoId Splash = "ImperialBSASplashWest";

    [DataField]
    public EntProtoId Impact = "ImperialBSAImpact";

    [ViewVariables]
    public bool Ready;

    [ViewVariables]
    public TimeSpan ReadyAt;

    [ViewVariables]
    public EntityUid? TopEntity;

    /// <summary>Задержка между приказом и ударом (как в старой Imperial-БСА: тревога, затем выстрел).</summary>
    [DataField]
    public TimeSpan FireDelay = TimeSpan.FromSeconds(9);

    [DataField]
    public SoundSpecifier AlertSound = new SoundPathSpecifier("/Audio/Corvax/Adminbuse/artillery.ogg");

    [ViewVariables]
    public EntityUid? PendingTarget;

    [ViewVariables]
    public EntityUid? PendingUser;

    [ViewVariables]
    public TimeSpan PendingFireAt;
}
