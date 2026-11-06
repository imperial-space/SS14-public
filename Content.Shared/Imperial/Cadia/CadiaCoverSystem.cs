using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

public sealed class CadiaCoverSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const float TrenchPointBlank = 2.5f;
    private const uint TrenchMissPercent = 55;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaCoverComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<CadiaInTrenchComponent, PreventCollideEvent>(OnTrenchCollide);
    }

    /// <summary>В окопе: кто стреляет вплотную, попадает, а издали больше половины пуль уходит над головой.</summary>
    private void OnTrenchCollide(Entity<CadiaInTrenchComponent> ent, ref PreventCollideEvent args)
    {
        if (!TryComp<ProjectileComponent>(args.OtherEntity, out var projectile))
            return;
        if (projectile.Shooter is { } shooter && !TerminatingOrDeleted(shooter))
        {
            var here = _xform.GetMapCoordinates(ent);
            var from = _xform.GetMapCoordinates(shooter);
            if (here.MapId == from.MapId && (here.Position - from.Position).LengthSquared() <= TrenchPointBlank * TrenchPointBlank)
                return;
        }
        var roll = unchecked((uint)(args.OtherEntity.Id * 2654435761u + ent.Owner.Id * 40503u)) % 100u;
        if (roll < TrenchMissPercent)
            args.Cancelled = true;
    }

    private void OnPreventCollide(Entity<CadiaCoverComponent> ent, ref PreventCollideEvent args)
    {
        if (!TryComp<ProjectileComponent>(args.OtherEntity, out var projectile))
            return;

        // Свой мешок не мешает: кто стоит вплотную, стреляет поверх.
        if (projectile.Shooter is { } shooter && !TerminatingOrDeleted(shooter))
        {
            var here = _xform.GetMapCoordinates(ent);
            var from = _xform.GetMapCoordinates(shooter);
            if (ent.Comp.Inside is { } center && !TerminatingOrDeleted(center))
            {
                var inside = _xform.GetMapCoordinates(center);
                var offset = from.Position - inside.Position;
                if (from.MapId == inside.MapId && Math.Max(Math.Abs(offset.X), Math.Abs(offset.Y)) <= CompOrNull<CadiaPillboxComponent>(center)?.InteriorHalf)
                {
                    args.Cancelled = true;
                    return;
                }
            }
            if (here.MapId == from.MapId && (here.Position - from.Position).LengthSquared() <= ent.Comp.OverRange * ent.Comp.OverRange)
            {
                args.Cancelled = true;
                return;
            }
        }

        // Столкновение проверяется много раз за полёт: решение должно быть одним для этой пары пуля — укрытие.
        var roll = unchecked((uint)(args.OtherEntity.Id * 2654435761u + ent.Owner.Id * 40503u)) % 100u;
        if (roll >= ent.Comp.Block * 100f)
            args.Cancelled = true;
    }
}
