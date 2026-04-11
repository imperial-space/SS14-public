using Content.Server.Imperial.Cult.Components;
using Content.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Content.Shared.FixedPoint;
using Robust.Shared.Physics;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cult;

/// <summary>
/// Система пилона культа — периодически лечит культистов в радиусе и конвертирует рядом стоящие структуры.
/// </summary>
public sealed class PylonHealSystem : EntitySystem
{
    private const string CultMagicSound = "/Audio/Imperial/cult/effects/magic/cult_pylon.ogg";
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PylonHealCultistsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var pylon, out var xform))
        {
            var now = _timing.CurTime;
            var coords = xform.Coordinates;

            // ── Исцеление культистов ─────────────────────────────────────────
            if (now >= pylon.NextHeal)
            {
                pylon.NextHeal = now + pylon.HealInterval;
                var anyHealed = false;

                foreach (var target in _lookup.GetEntitiesInRange(uid, pylon.HealRadius, LookupFlags.Dynamic))
                {
                    if (target == uid) continue;
                    if (!HasComp<CultistComponent>(target) && !HasComp<CultConstructComponent>(target)) continue;
                    if (!TryComp<MobStateComponent>(target, out var mobState) || mobState.CurrentState != MobState.Alive) continue;
                    if (!TryComp<DamageableComponent>(target, out var damageable)) continue;

                    var bruteTypes = new HashSet<string> { "Blunt", "Slash", "Piercing" };
                    var burnTypes = new HashSet<string> { "Heat", "Shock", "Cold", "Caustic" };
                    var currentDamage = _damage.GetPositiveDamage((target, damageable));
                    var heal = new DamageSpecifier();
                    foreach (var (dt, dmg) in currentDamage.DamageDict)
                    {
                        if (dmg <= FixedPoint2.Zero) continue;
                        if (bruteTypes.Contains(dt))
                            heal.DamageDict[dt] = -(FixedPoint2)pylon.HealBrute;
                        else if (burnTypes.Contains(dt))
                            heal.DamageDict[dt] = -(FixedPoint2)pylon.HealBurn;
                        else if (dt == "Bloodloss")
                            heal.DamageDict[dt] = -(FixedPoint2)pylon.HealBloodloss;
                    }
                    if (!heal.Empty)
                        _damage.TryChangeDamage(target, heal, ignoreResistances: true, interruptsDoAfters: false);
                    _popup.PopupEntity(Loc.GetString("cult-pylon-heal"), target, target, PopupType.Small);
                    anyHealed = true;
                }

                if (anyHealed)
                    _audio.PlayPvs(CultMagicSound, uid);
            }

            // ── Конвертация структур ─────────────────────────────────────────
            if (pylon.ConversionMap.Count > 0 && now >= pylon.NextConvert)
            {
                pylon.NextConvert = now + pylon.ConvertInterval;
                ConvertNearbyStructures(uid, pylon, coords);
            }
        }
    }

    private void ConvertNearbyStructures(EntityUid pylonUid, PylonHealCultistsComponent pylon, EntityCoordinates pylonCoords)
    {
        // Собираем кандидатов заранее (нельзя удалять во время перебора)
        var candidates = new List<(EntityUid Ent, string CultProto, EntityCoordinates Coords)>();

        foreach (var ent in _lookup.GetEntitiesInRange(pylonCoords, pylon.ConvertRadius))
        {
            if (ent == pylonUid) continue;
            var meta = MetaData(ent);
            if (meta.EntityPrototype?.ID is not {} protoId) continue;
            if (!pylon.ConversionMap.TryGetValue(protoId, out var cultProto)) continue;
            candidates.Add((ent, cultProto, Transform(ent).Coordinates));
        }

        foreach (var (ent, cultProto, entCoords) in candidates)
        {
            QueueDel(ent);
            Spawn(cultProto, entCoords);
        }
    }
}
