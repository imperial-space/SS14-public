using System.Numerics;
using Content.Shared.Damage;

namespace Content.Shared.Weapons.Melee;

[ByRefEvent]
public record struct MeleeDamageDealtEvent(EntityUid Target, EntityUid User, EntityUid Weapon, DamageSpecifier Damage);
