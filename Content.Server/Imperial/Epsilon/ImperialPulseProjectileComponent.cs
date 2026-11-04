using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.Epsilon;

/// <summary>
/// /obj/projectile/beam/pulse: попав в стену или постройку, импульс сносит её (ex_act heavy)
/// и летит дальше, пока не исчерпает запас пробития; по живым бьёт обычным уроном и гаснет.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialPulseProjectileComponent : Component
{
    /// <summary>Сколько построек импульс сносит насквозь; на последней останавливается.</summary>
    [DataField]
    public int Pierce = 3;

    /// <summary>Урон по постройке — с запасом на любую стену.</summary>
    [DataField]
    public float StructureDamage = 5000f;

    /// <summary>Клетки, где импульс уже снёс постройку: балки от снесённой стены не тратят запас.</summary>
    [ViewVariables]
    public HashSet<(EntityUid Grid, Vector2i Tile)> PiercedTiles = new();
}
