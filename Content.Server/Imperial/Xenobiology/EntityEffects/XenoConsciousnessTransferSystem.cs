using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Xenobiology.EntityEffects;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;

namespace Content.Server.Imperial.Xenobiology.EntityEffects;

/// <summary>
/// Зелье переноса сознания (Радужная кровь).
/// При поглощении переносит разум игрока в ближайшее живое существо в радиусе <see cref="XenoConsciousnessTransferEffect.Range"/> тайлов.
/// </summary>
public sealed partial class XenoConsciousnessTransferSystem
    : EntityEffectSystem<MetaDataComponent, XenoConsciousnessTransferEffect>
{
    [Dependency] private readonly EntityLookupSystem _lookup   = default!;
    [Dependency] private readonly MindSystem         _mind     = default!;
    [Dependency] private readonly MobStateSystem     _mobState = default!;
    [Dependency] private readonly PopupSystem        _popup    = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<XenoConsciousnessTransferEffect> args)
    {
        var uid = entity.Owner;

        // Нужен разум — только игроки имеют разум
        if (!_mind.TryGetMind(uid, out var mindId, out var mind))
        {
            _popup.PopupEntity(
                Loc.GetString("xeno-consciousness-transfer-no-mind"),
                uid, PopupType.SmallCaution);
            return;
        }

        // Ищем живые существа в радиусе
        var range = args.Effect.Range;
        var candidates = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(uid, range, candidates);

        EntityUid? target = null;
        var origin = _xform.GetMapCoordinates(uid);
        var bestDistance = float.MaxValue;
        foreach (var candidateUid in candidates)
        {
            // Пропускаем себя и мёртвых
            if (candidateUid == uid)
                continue;

            if (!TryComp<MobStateComponent>(candidateUid, out var candidateMob))
                continue;

            if (!_mobState.IsAlive(candidateUid, candidateMob))
                continue;

            // Пропускаем тех у кого уже есть разум (другой игрок)
            if (_mind.TryGetMind(candidateUid, out _, out _))
                continue;

            var candidateCoords = _xform.GetMapCoordinates(candidateUid);
            if (candidateCoords.MapId != origin.MapId)
                continue;

            var distance = (candidateCoords.Position - origin.Position).LengthSquared();
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            target = candidateUid;
        }

        if (target == null)
        {
            _popup.PopupEntity(
                Loc.GetString("xeno-consciousness-transfer-no-target"),
                uid, PopupType.SmallCaution);
            return;
        }

        // Переносим разум
        _mind.TransferTo(mindId, target.Value, ghostCheckOverride: true, createGhost: false, mind: mind);

        _popup.PopupEntity(
            Loc.GetString("xeno-consciousness-transfer-success"),
            target.Value, PopupType.Large);
    }
}
