using System.Linq;
using System.Numerics;
using Content.Server.Polymorph.Systems;
using Content.Shared.Administration.Systems;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Doors.Components;
using Content.Shared.Examine;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Storage.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>Эффект магического снаряда (/obj/projectile/magic/*).</summary>
public enum ImperialMagicBolt : byte
{
    Nothing,
    Death,
    Resurrection,
    Teleport,
    Safety,
    Door,
    Change,
    Animate,
    Locker,
    Flying,
    Fetch,
    Antimagic,
    Babel,
    Necropotence,
    Fireball,
    Shrink,
    Chill,
    Freeze,
    Swap,
    Tentacle,
    Booze,
    Hallucination,
    Pax,
    Pizza,
    Prank,
    Repulse,
    Zap,
    Plague,
    Bald,
    Levitate,
    Rebellion,
    Bounty,
    Wipe,

    /// <summary>Посох хаоса: случайный эффект из списка allowed_projectile_types.</summary>
    Chaos,
}
