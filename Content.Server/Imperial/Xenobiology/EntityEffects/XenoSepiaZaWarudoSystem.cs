using Content.Server.Popups;
using Content.Shared.EntityEffects;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Imperial.Xenobiology.EntityEffects;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Xenobiology.EntityEffects;

/// <summary>
/// Система зелья сепии (ZA WARUDO).
///
/// При метаболизации: добавляет <see cref="XenoZaWarudoPendingComponent"/> с обратным отсчётом.
/// В <see cref="Update"/>: по истечении задержки — парализует всех существ в радиусе
/// <see cref="XenoSepiaZaWarudoEffect.Range"/> на <see cref="XenoSepiaZaWarudoEffect.StunDuration"/> секунд.
/// </summary>
public sealed partial class XenoSepiaZaWarudoSystem
    : EntityEffectSystem<MetaDataComponent, XenoSepiaZaWarudoEffect>
{
    [Dependency] private readonly EntityLookupSystem  _lookup  = default!;
    [Dependency] private readonly SharedStunSystem    _stun    = default!;
    [Dependency] private readonly PopupSystem         _popup   = default!;
    [Dependency] private readonly TransformSystem     _xform   = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<XenoSepiaZaWarudoEffect> args)
    {
        var uid = entity.Owner;

        // Не применять повторно если уже ожидает
        if (HasComp<XenoZaWarudoPendingComponent>(uid))
            return;

        var effect = args.Effect;
        var coords = _xform.GetMapCoordinates(uid);

        var pending = AddComp<XenoZaWarudoPendingComponent>(uid);
        pending.TimeRemaining = effect.Delay;
        pending.EffectCenter   = coords;
        pending.Range          = effect.Range;
        pending.StunDuration   = effect.StunDuration;

        _popup.PopupEntity(
            Loc.GetString("xeno-sepia-za-warudo-incoming"),
            uid, PopupType.LargeCaution);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<XenoZaWarudoPendingComponent>();
        while (query.MoveNext(out var uid, out var pending))
        {
            pending.TimeRemaining -= frameTime;
            if (pending.TimeRemaining > 0f)
                continue;

            // Таймер истёк — применяем эффект
            RemComp<XenoZaWarudoPendingComponent>(uid);

            var center = pending.EffectCenter;
            var stunTime = TimeSpan.FromSeconds(pending.StunDuration);

            // Получаем всех мобов в радиусе
            var targets = new HashSet<Entity<MobStateComponent>>();
            _lookup.GetEntitiesInRange(center, pending.Range, targets);

            foreach (var (targetUid, _) in targets)
            {
                _stun.TryKnockdown(targetUid, stunTime, refresh: true, autoStand: false, drop: true, force: true);
            }

            // Показываем сообщение самому применившему (он тоже попал в радиус)
            _popup.PopupEntity(
                Loc.GetString("xeno-sepia-za-warudo-activated"),
                uid, PopupType.Large);
        }
    }
}
