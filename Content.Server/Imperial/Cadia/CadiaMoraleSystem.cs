using Content.Server.Popups;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Мораль: раз в секунду считает ауры и восстановление, реагирует на раны, гибель соседей и удары с неба.
/// Шаткий боец медлит, сломленный ещё и не всегда может выстрелить, а на нуле падает в панику.
/// </summary>
public sealed class CadiaMoraleSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly Robust.Shared.Audio.Systems.SharedAudioSystem _audio = default!;

    private const float WoundThreshold = 25f;
    private const float WoundShock = -6f;
    private const float DeathRadius = 7f;
    private const float AllyDeathShock = -8f;
    private const float EnemyDeathLift = 3f;
    private const float PanicRecover = 20f;
    private const float MaxFearPerSecond = -5f;
    private static readonly TimeSpan PanicCooldown = TimeSpan.FromSeconds(20);
    private static readonly SoundSpecifier MedicCallSound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");

    private readonly HashSet<Entity<CadiaMoraleComponent>> _nearby = new();
    private readonly Dictionary<EntityUid, float> _pending = new();
    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaMoraleComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<CadiaMoraleComponent, MobStateChangedEvent>(OnMobState);
    }

    /// <summary>Изменить мораль одного бойца, с порогами и надписями.</summary>
    public void Change(EntityUid uid, float delta)
    {
        if (!TryComp<CadiaMoraleComponent>(uid, out var morale) || HasComp<CadiaAstartesComponent>(uid) || !_mobState.IsAlive(uid))
            return;

        var before = morale.Morale;
        morale.Morale = Math.Clamp(before + delta, 0f, 100f);
        if (Math.Abs(morale.Morale - before) < 0.001f)
            return;
        Dirty(uid, morale);

        if (morale.Morale <= 0f)
        {
            morale.Morale = PanicRecover;
            // Паника не должна запирать бойца намертво: повторно только через паузу.
            var now = _timing.CurTime;
            if (now >= morale.NextPanic)
            {
                morale.NextPanic = now + PanicCooldown;
                _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(2.5));
                _popup.PopupEntity(Loc.GetString("cadia-morale-panic"), uid, uid, PopupType.LargeCaution);
                _popup.PopupEntity(Loc.GetString("cadia-morale-panic-others", ("name", Name(uid))), uid, Robust.Shared.Player.Filter.PvsExcept(uid), true, PopupType.Medium);
            }
        }
        else if (Crossed(before, morale.Morale, CadiaMoraleComponent.BrokenBelow))
        {
            _popup.PopupEntity(Loc.GetString("cadia-morale-broken"), uid, uid, PopupType.MediumCaution);
        }
        else if (Crossed(before, morale.Morale, CadiaMoraleComponent.ShakenBelow))
        {
            _popup.PopupEntity(Loc.GetString("cadia-morale-shaken"), uid, uid, PopupType.SmallCaution);
        }
        else if (before < CadiaMoraleComponent.ShakenBelow && morale.Morale >= CadiaMoraleComponent.ShakenBelow)
        {
            _popup.PopupEntity(Loc.GetString("cadia-morale-steady"), uid, uid);
        }

        if (ZoneOf(before) != ZoneOf(morale.Morale))
            _speed.RefreshMovementSpeedModifiers(uid);
    }

    /// <summary>Удар рядом: обстрел, бомба, большой взрыв.</summary>
    public void Shock(MapCoordinates at, float radius, float delta)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(at, radius, _nearby);
        foreach (var soldier in _nearby)
            Change(soldier, delta);
    }

    /// <summary>Тяжелораненый: все медики его стороны на этой карте слышат вызов и видят крест на экране.</summary>
    private void CallMedics(EntityUid wounded)
    {
        if (!TryComp<CadiaFactionMemberComponent>(wounded, out var side))
            return;
        var from = _xform.GetMapCoordinates(wounded);
        var medics = EntityQueryEnumerator<CadiaMedicSightComponent, CadiaFactionMemberComponent, TransformComponent>();
        while (medics.MoveNext(out var uid, out _, out var member, out var xform))
        {
            if (uid == wounded || member.Faction != side.Faction || xform.MapID != from.MapId)
                continue;
            // Крест на экране виден только вблизи, поэтому дальнему медику говорим, куда бежать.
            var delta = from.Position - _xform.GetWorldPosition(xform);
            var side8 = delta.ToWorldAngle().GetDir().ToString().ToLowerInvariant();
            _popup.PopupEntity(Loc.GetString("cadia-medic-call", ("name", Name(wounded)), ("dir", Loc.GetString($"cadia-dir-{side8}")), ("meters", (int) delta.Length())), uid, uid, PopupType.Medium);
            _audio.PlayEntity(MedicCallSound, Robust.Shared.Player.Filter.Entities(uid), uid, false);
        }
    }

    private static bool Crossed(float before, float after, float line) => before >= line && after < line;

    private static int ZoneOf(float morale)
    {
        return morale < CadiaMoraleComponent.BrokenBelow ? 2 : morale < CadiaMoraleComponent.ShakenBelow ? 1 : 0;
    }

    private void OnDamaged(Entity<CadiaMoraleComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased && args.DamageDelta is { } delta && delta.GetTotal() >= WoundThreshold)
            Change(ent, WoundShock);
    }

    private void OnMobState(Entity<CadiaMoraleComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Critical)
            CallMedics(ent);
        if (args.NewMobState != MobState.Dead)
            return;
        var faction = CompOrNull<CadiaFactionMemberComponent>(ent)?.Faction;
        _nearby.Clear();
        _lookup.GetEntitiesInRange(_xform.GetMapCoordinates(ent), DeathRadius, _nearby);
        foreach (var soldier in _nearby)
        {
            if (soldier.Owner == ent.Owner)
                continue;
            var same = CompOrNull<CadiaFactionMemberComponent>(soldier)?.Faction == faction;
            Change(soldier, same ? AllyDeathShock : EnemyDeathLift);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextTick)
            return;
        _nextTick = now + TimeSpan.FromSeconds(1);
        _pending.Clear();

        var auras = EntityQueryEnumerator<CadiaMoraleAuraComponent, TransformComponent>();
        while (auras.MoveNext(out var uid, out var aura, out var xform))
        {
            // Штандарт работает только в руках: его сторона — сторона несущего.
            var holder = HasComp<CadiaFactionMemberComponent>(uid) ? uid : xform.ParentUid;
            // Постройка (алтарь) действует, пока стоит, а носитель должен быть жив.
            var faction = aura.Faction;
            if (faction == null)
            {
                if (!TryComp<CadiaFactionMemberComponent>(holder, out var side) || !_mobState.IsAlive(holder))
                    continue;
                faction = side.Faction;
            }
            _nearby.Clear();
            _lookup.GetEntitiesInRange(_xform.GetMapCoordinates(uid, xform), aura.Radius, _nearby);
            foreach (var soldier in _nearby)
            {
                if (soldier.Owner == holder)
                    continue;
                var same = CompOrNull<CadiaFactionMemberComponent>(soldier)?.Faction == faction;
                var rate = same ? aura.FriendRate : aura.EnemyRate;
                if (rate != 0f)
                    _pending[soldier] = _pending.GetValueOrDefault(soldier) + rate;
            }
        }

        var query = EntityQueryEnumerator<CadiaMoraleComponent>();
        while (query.MoveNext(out var uid, out var morale))
        {
            // Страх от многих источников не суммируется до мгновенной паники.
            var delta = Math.Max(_pending.GetValueOrDefault(uid), MaxFearPerSecond);
            if (morale.Morale < morale.RecoverTo)
                delta += morale.RecoverRate;
            if (delta != 0f)
                _pending[uid] = delta;
        }
        foreach (var (uid, delta) in _pending)
            Change(uid, delta);
    }
}
