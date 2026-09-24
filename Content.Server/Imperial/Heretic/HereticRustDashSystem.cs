using System.Numerics;
using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticRustDashSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform   = default!;
    [Dependency] private readonly EntityLookupSystem    _lookup  = default!;
    [Dependency] private readonly DamageableSystem      _damage  = default!;
    [Dependency] private readonly PopupSystem           _popup   = default!;
    [Dependency] private readonly SharedAudioSystem     _audio   = default!;
    [Dependency] private readonly IRobustRandom         _random  = default!;

    private static readonly string[] RuneEffects =
    {
        "HereticSmallRuneEffect1", "HereticSmallRuneEffect4",
        "HereticSmallRuneEffect7", "HereticSmallRuneEffect10"
    };

    private readonly HashSet<EntityUid> _hitBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticComponent, HereticRustDashActionEvent>(OnRustDash);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<HereticRustDashComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.IsWaiting)
            {
                comp.WaitAccum += frameTime;
                if (comp.WaitAccum >= comp.DashWaitDuration)
                {
                    comp.IsWaiting = false;
                    comp.IsActive = true;
                }
                continue;
            }

            if (!comp.IsActive) continue;
            ProcessDash(uid, comp, frameTime);
        }
    }

    private void OnRustDash(Entity<HereticComponent> ent, ref HereticRustDashActionEvent args)
    {
        if (args.Handled) return;

        var mapCoords = _xform.GetMapCoordinates(ent.Owner);
        if (_lookup.GetEntitiesInRange<HereticRustOverlayComponent>(mapCoords, 0.6f).Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-rust-dash-no-rust"), ent.Owner, ent.Owner, PopupType.Medium);
            return;
        }

        if (HasComp<HereticRustDashComponent>(ent.Owner)) return;

        args.Handled = true;

        var comp = AddComp<HereticRustDashComponent>(ent.Owner);
        comp.Destination = args.Target;
        comp.IsWaiting   = true;
        comp.IsActive    = false;
        comp.WaitAccum   = 0f;
        comp.TrailAccum  = 0f;
        comp.RustAccum   = 0f;
        comp.DamageAccum = 0f;

        Spawn(comp.DashMarkerPrototype, args.Target);
        _audio.PlayPvs(comp.DashSound, ent.Owner);
    }

    private void ProcessDash(EntityUid uid, HereticRustDashComponent comp, float frameTime)
    {
        var myMap   = _xform.GetMapCoordinates(uid);
        var destMap = _xform.ToMapCoordinates(comp.Destination);
        var dir  = destMap.Position - myMap.Position;
        var dist = dir.Length();

        if (dist < 0.3f) { FinishDash(uid); return; }

        var dirNorm = dir / dist;
        var step    = MathF.Min(comp.DashSpeed * frameTime, dist);
        _xform.SetWorldPosition(uid, myMap.Position + dirNorm * step);

        var nowMap = _xform.GetMapCoordinates(uid);

        comp.TrailAccum += frameTime;
        if (comp.TrailAccum >= comp.TrailInterval)
        {
            comp.TrailAccum = 0f;
            Spawn(comp.DashTrailPrototype, nowMap);
        }

        comp.RustAccum += frameTime;
        if (comp.RustAccum >= comp.RustInterval)
        {
            comp.RustAccum = 0f;
            SpawnRustOverlayAt(nowMap);
            Spawn(_random.Pick(RuneEffects), nowMap);
        }

        comp.DamageAccum += frameTime;
        if (comp.DamageAccum >= comp.DamageInterval)
        {
            comp.DamageAccum = 0f;
            HitEntitiesNear(uid, nowMap, comp);
        }
    }

    private void HitEntitiesNear(EntityUid caster, MapCoordinates pos, HereticRustDashComponent comp)
    {
        _hitBuffer.Clear();
        _lookup.GetEntitiesInRange(pos.MapId, pos.Position, comp.HitRadius, _hitBuffer);

        foreach (var ent in _hitBuffer)
        {
            if (ent == caster) continue;

            var protoId = MetaData(ent).EntityPrototype?.ID;
            if (protoId is "HereticRustWall" or "WallSolidRust" or "WallReinforcedRust")
            {
                QueueDel(ent);
                continue;
            }

            if (!HasComp<MobStateComponent>(ent)) continue;

            var dmg = new DamageSpecifier();
            dmg.DamageDict["Caustic"] = FixedPoint2.New(comp.DashDamage);
            _damage.TryChangeDamage(ent, dmg, ignoreResistances: false);
        }
    }

    private void SpawnRustOverlayAt(MapCoordinates pos)
    {
        if (_lookup.GetEntitiesInRange<HereticRustOverlayComponent>(pos, 0.4f).Count > 0) return;
        Spawn("HereticRustOverlay", pos);
    }

    private void FinishDash(EntityUid uid) => RemComp<HereticRustDashComponent>(uid);
}
