using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Server.Weapons.Melee;
using Content.Shared.Actions;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.MartialArts;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Popups;
using Content.Shared.Pulling.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.MartialArts;

/// <summary>Кто нанёс удар, чтобы правильно посчитать блок (attack_type в check_block).</summary>
public enum MartialAttackType : byte
{
    Unarmed,
    Melee,
    Projectile,
    Thrown,
}
