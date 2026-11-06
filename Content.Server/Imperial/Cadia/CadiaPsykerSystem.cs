using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Stunnable;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Псайкеры обеих сторон: варп-заряд, напряжение и Опасности варпа. Пилоны делают силы Хаоса дороже и рискованнее,
/// неприкасаемые глушат силы полностью. Сами силы — действия с <see cref="CadiaPsyPowerComponent"/>.
/// </summary>
public sealed class CadiaPsykerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CadiaPylonVeilSystem _veil = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly CadiaHoloTableSystem _holo = default!;

    private static readonly SoundSpecifier PerilsSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/curse.ogg");
    private static readonly SoundSpecifier SilencedSound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg");
    private static readonly SoundSpecifier ScreamSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/blind.ogg");
    private static readonly SoundSpecifier SmiteSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/lightning_chargeup.ogg");
    private static readonly SoundSpecifier DomeSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/forcewall.ogg");
    private static readonly SoundSpecifier FireSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/fireball.ogg");
    private static readonly SoundSpecifier PlagueSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/cosmic_energy.ogg");
    private static readonly SoundSpecifier BloodSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/demon_consume.ogg");
    private static readonly SoundSpecifier VeilSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/ethereal_enter.ogg");
    private static readonly SoundSpecifier PortalSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/castsummon.ogg");

    private const string EscapedDaemon = "CadiaMobBlueHorror";
    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private readonly HashSet<Entity<CadiaNullZoneComponent>> _nulls = new();
    private TimeSpan _nextTick;

    private static readonly EntProtoId CadiaPsySmiteEffect = "CadiaPsySmiteEffect";
    private static readonly EntProtoId CadiaPsyBloodEffect = "CadiaPsyBloodEffect";
    private static readonly EntProtoId CadiaWarpRiftSmall = "CadiaWarpRiftSmall";
    private static readonly EntProtoId CadiaPsyPerilsEffect = "CadiaPsyPerilsEffect";
    private static readonly EntProtoId CadiaPsyDomePlate = "CadiaPsyDomePlate";
    private static readonly EntProtoId CadiaPsyRingBlue = "CadiaPsyRingBlue";
    private static readonly EntProtoId CadiaPsyFuryEffect = "CadiaPsyFuryEffect";
    private static readonly EntProtoId CadiaPsyWarpfireEffect = "CadiaPsyWarpfireEffect";
    private static readonly EntProtoId CadiaPsyPlagueEffect = "CadiaPsyPlagueEffect";
    private static readonly EntProtoId CadiaPsyRingPink = "CadiaPsyRingPink";

    private static readonly TimeSpan TickDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ScreamDuration = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan SilencedDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan VeilDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan CharmDuration = TimeSpan.FromSeconds(3.5);

    private const float NullAroundRange = 14f;
    private const float ScreamRadius = 5f;
    private const float ScreamAmount = 8f;
    private const float ForesightRadius = 30f;
    private const float FuryRange = 1.2f;
    private const float FuryAmount = 30f;
    private const float WarpfireRange = 2f;
    private const float WarpfireAmount = 22f;
    private const float WarpfireAmount2 = 6f;
    private const float PlagueRadius = 4f;
    private const float PlagueAmount = 16f;
    private const float PlagueAmount2 = 6f;
    private const float CharmRadius = 4.5f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaPsykerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CadiaPsykerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CadiaPsykerComponent, CadiaPsyInstantEvent>(OnInstant);
        SubscribeLocalEvent<CadiaPsykerComponent, CadiaPsyEntityEvent>(OnEntity);
        SubscribeLocalEvent<CadiaPsykerComponent, CadiaPsyWorldEvent>(OnWorld);
    }

    private void OnMapInit(Entity<CadiaPsykerComponent> ent, ref MapInitEvent args)
    {
        GrantPowers(ent);
    }

    /// <summary>Компонент роли приходит уже после инициализации моба — силы выдаются и отсюда.</summary>
    public void GrantPowers(Entity<CadiaPsykerComponent> ent)
    {
        if (ent.Comp.Actions.Count > 0)
            return;
        foreach (var power in ent.Comp.Powers)
        {
            if (_actions.AddAction(ent, power) is { } action)
                ent.Comp.Actions.Add(action);
        }
    }

    private void OnShutdown(Entity<CadiaPsykerComponent> ent, ref ComponentShutdown args)
    {
        foreach (var action in ent.Comp.Actions)
            _actions.RemoveAction(ent.Owner, action);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextTick)
            return;
        _nextTick = now + TickDelay;

        var query = EntityQueryEnumerator<CadiaPsykerComponent>();
        while (query.MoveNext(out var uid, out var psyker))
        {
            var charge = Math.Min(psyker.MaxCharge, psyker.Charge + psyker.Regen);
            var strain = Math.Max(0f, psyker.Strain - psyker.StrainDecay);
            if (charge == psyker.Charge && strain == psyker.Strain)
                continue;
            psyker.Charge = charge;
            psyker.Strain = strain;
            Dirty(uid, psyker);
        }

        var veils = EntityQueryEnumerator<CadiaVeilComponent>();
        while (veils.MoveNext(out var uid, out var veil))
        {
            if (now < veil.Until)
                continue;
            RemCompDeferred<CadiaVeilComponent>(uid);
            RemCompDeferred<StealthComponent>(uid);
        }
    }

    /// <summary>Снять напряжение (молитва священника).</summary>
    public void Soothe(Entity<CadiaPsykerComponent> ent, float amount)
    {
        ent.Comp.Strain = Math.Max(0f, ent.Comp.Strain - amount);
        Dirty(ent);
    }

    // ═══════════════════════ Каст ═══════════════════════

    private void OnInstant(Entity<CadiaPsykerComponent> ent, ref CadiaPsyInstantEvent args)
    {
        if (args.Handled || _veil.Blocks(ent, "cadia-veil-warp", false) || !TryComp<CadiaPsyPowerComponent>(args.Action, out var power) || !TryBegin(ent, power))
            return;
        args.Handled = true;
        switch (power.Power)
        {
            case CadiaPsyPower.Dome:
                Dome(ent);
                break;
            case CadiaPsyPower.Scream:
                Scream(ent);
                break;
            case CadiaPsyPower.Foresight:
                Foresight(ent);
                break;
            case CadiaPsyPower.Veil:
                Veil(ent);
                break;
            case CadiaPsyPower.Plague:
                Plague(ent);
                break;
            case CadiaPsyPower.Charm:
                Charm(ent);
                break;
        }
        Finish(ent, power);
    }

    private void OnEntity(Entity<CadiaPsykerComponent> ent, ref CadiaPsyEntityEvent args)
    {
        if (args.Handled || _veil.Blocks(ent, "cadia-veil-warp", false) || !TryComp<CadiaPsyPowerComponent>(args.Action, out var power) || !TryBegin(ent, power))
            return;
        args.Handled = true;
        switch (power.Power)
        {
            case CadiaPsyPower.Smite:
                Hurt(args.Target, ent, "Shock", ent.Comp.EntitySmiteAmount);
                Spawn(CadiaPsySmiteEffect, _xform.GetMapCoordinates(args.Target));
                _audio.PlayPvs(SmiteSound, args.Target);
                break;
            case CadiaPsyPower.Suppression:
                Suppress(ent, args.Target);
                break;
            case CadiaPsyPower.Bloodboil:
                Hurt(args.Target, ent, "Heat", ent.Comp.EntityBloodboilAmount);
                Hurt(args.Target, ent, "Slash", ent.Comp.EntityBloodboilAmount2);
                _damage.HealEvenly(ent.Owner, ent.Comp.EntityBloodboilAmount3, origin: ent);
                Spawn(CadiaPsyBloodEffect, _xform.GetMapCoordinates(args.Target));
                _audio.PlayPvs(BloodSound, args.Target);
                break;
        }
        Finish(ent, power);
    }

    private void OnWorld(Entity<CadiaPsykerComponent> ent, ref CadiaPsyWorldEvent args)
    {
        if (args.Handled || _veil.Blocks(ent, "cadia-veil-warp", false) || !TryComp<CadiaPsyPowerComponent>(args.Action, out var power) || !TryBegin(ent, power))
            return;
        args.Handled = true;
        var target = _xform.ToMapCoordinates(args.Target);
        switch (power.Power)
        {
            case CadiaPsyPower.Fury:
                Fury(ent, target);
                break;
            case CadiaPsyPower.Warpfire:
                Warpfire(ent, target);
                break;
            case CadiaPsyPower.Portal:
                Spawn(CadiaWarpRiftSmall, target);
                _audio.PlayPvs(PortalSound, ent);
                break;
        }
        Finish(ent, power);
    }

    /// <summary>Хватает ли заряда, не подавлен ли, не рядом ли неприкасаемый. Заряд списывается здесь.</summary>
    private bool TryBegin(Entity<CadiaPsykerComponent> ent, CadiaPsyPowerComponent power)
    {
        if (_timing.CurTime < ent.Comp.SilencedUntil)
        {
            Deny(ent, "cadia-psy-silenced");
            return false;
        }
        var (total, pylon) = NullAround(ent);
        if (total)
        {
            Deny(ent, "cadia-psy-nulled");
            return false;
        }
        var chaos = TryComp<CadiaFactionMemberComponent>(ent, out var member) && member.Faction == CadiaFaction.Chaos;
        var cost = power.Cost * (chaos && pylon ? 1.5f : 1f);
        if (ent.Comp.Charge < cost)
        {
            Deny(ent, "cadia-psy-no-charge");
            return false;
        }
        ent.Comp.Charge -= cost;
        Dirty(ent);
        return true;
    }

    private void Deny(EntityUid user, string key)
    {
        _popup.PopupEntity(Loc.GetString(key), user, user, PopupType.SmallCaution);
        _audio.PlayEntity(SilencedSound, Robust.Shared.Player.Filter.Entities(user), user, false);
    }

    private (bool Total, bool Pylon) NullAround(EntityUid user)
    {
        var total = false;
        var pylon = false;
        _nulls.Clear();
        _lookup.GetEntitiesInRange(_xform.GetMapCoordinates(user), NullAroundRange, _nulls);
        var here = _xform.GetWorldPosition(user);
        foreach (var zone in _nulls)
        {
            if (zone.Owner == user || (_xform.GetWorldPosition(zone) - here).Length() > zone.Comp.Radius)
                continue;
            if (zone.Comp.Total)
                total = true;
            else
                pylon = true;
        }
        return (total, pylon);
    }

    /// <summary>После силы: растёт напряжение и бросается кость Опасностей варпа.</summary>
    private void Finish(Entity<CadiaPsykerComponent> ent, CadiaPsyPowerComponent power)
    {
        var (_, pylon) = NullAround(ent);
        var chaos = TryComp<CadiaFactionMemberComponent>(ent, out var member) && member.Faction == CadiaFaction.Chaos;

        ent.Comp.Strain = Math.Min(100f, ent.Comp.Strain + power.Strain);
        Dirty(ent);

        var risk = Math.Clamp((ent.Comp.Strain - 30f) / 120f, 0f, 0.6f);
        if (chaos && pylon)
            risk += 0.15f;
        if (HasHood(ent))
            risk *= 0.5f;
        if (_random.Prob(risk))
            Perils(ent);
    }

    private bool HasHood(EntityUid user)
    {
        return _inventory.TryGetSlotEntity(user, "outerClothing", out var robe) && HasComp<CadiaPsychicHoodComponent>(robe);
    }

    /// <summary>Опасности варпа: чем выше напряжение, тем страшнее — от ожога разума до вырвавшегося демона.</summary>
    private void Perils(Entity<CadiaPsykerComponent> ent)
    {
        _audio.PlayPvs(PerilsSound, ent);
        var coords = _xform.GetMapCoordinates(ent);
        Spawn(CadiaPsyPerilsEffect, coords);
        _popup.PopupEntity(Loc.GetString("cadia-psy-perils"), ent, PopupType.LargeCaution);

        var roll = _random.NextFloat() * ent.Comp.Strain;
        if (roll < 40f)
        {
            Hurt(ent, null, "Cellular", ent.Comp.PerilsAmount);
            _stun.TryUpdateParalyzeDuration(ent, ent.Comp.PerilsDuration);
        }
        else if (roll < 75f)
        {
            _explosion.QueueExplosion(coords, "Default", ent.Comp.PerilsTotalIntensity, ent.Comp.PerilsSlope, ent.Comp.PerilsMaxTileIntensity, ent, canCreateVacuum: false);
            ent.Comp.Strain = 40f;
            Dirty(ent);
        }
        else
        {
            Spawn(EscapedDaemon, coords.Offset(_random.NextVector2(1.2f)));
            Hurt(ent, null, "Cellular", ent.Comp.PerilsAmount2);
            _stun.TryUpdateParalyzeDuration(ent, ent.Comp.PerilsDuration2);
            ent.Comp.Strain = 20f;
            Dirty(ent);
        }
    }

    // ═══════════════════════ Силы ═══════════════════════

    private void Hurt(EntityUid target, EntityUid? source, string type, float amount)
    {
        if (!_proto.TryIndex<DamageTypePrototype>(type, out var proto))
            return;
        _damage.TryChangeDamage(target, new DamageSpecifier(proto, amount), origin: source);
    }

    /// <summary>Враги в радиусе, без себя и своих.</summary>
    private IEnumerable<EntityUid> Enemies(EntityUid user, float radius)
    {
        var faction = CompOrNull<CadiaFactionMemberComponent>(user)?.Faction;
        _mobs.Clear();
        _lookup.GetEntitiesInRange(_xform.GetMapCoordinates(user), radius, _mobs);
        foreach (var mob in _mobs)
        {
            if (mob.Owner == user)
                continue;
            if (faction != null && TryComp<CadiaFactionMemberComponent>(mob, out var other) && other.Faction == faction)
                continue;
            yield return mob;
        }
    }

    /// <summary>Купол: кольцо силовых плит вокруг псайкера, снаряды и пехота не проходят.</summary>
    private void Dome(EntityUid user)
    {
        var center = _xform.GetMapCoordinates(user);
        const int Plates = 16;
        for (var i = 0; i < Plates; i++)
        {
            var dir = Angle.FromDegrees(i * 360f / Plates).ToVec();
            Spawn(CadiaPsyDomePlate, center.Offset(dir * 2.5f));
        }
        _audio.PlayPvs(DomeSound, user);
    }

    private void Scream(EntityUid user)
    {
        foreach (var enemy in Enemies(user, ScreamRadius))
        {
            Hurt(enemy, user, "Cellular", ScreamAmount);
            _stun.TryUpdateParalyzeDuration(enemy, ScreamDuration);
        }
        Spawn(CadiaPsyRingBlue, _xform.GetMapCoordinates(user));
        _audio.PlayPvs(ScreamSound, user);
    }

    /// <summary>Предвидение: все враги в большом радиусе отмечаются на голокарте стороны.</summary>
    private void Foresight(EntityUid user)
    {
        if (!TryComp<CadiaFactionMemberComponent>(user, out var member))
            return;
        var count = 0;
        var pos = _xform.GetMapCoordinates(user);
        foreach (var enemy in Enemies(user, ForesightRadius))
        {
            var point = _xform.GetMapCoordinates(enemy);
            _holo.MarkEnemy(member.Faction, pos.MapId, point.Position, Name(user));
            if (++count >= 40)
                break;
        }
        Spawn(CadiaPsyRingBlue, pos);
        _audio.PlayPvs(SmiteSound, user);
        _popup.PopupEntity(Loc.GetString("cadia-psy-foresight", ("count", count)), user, user, PopupType.Medium);
    }

    /// <summary>Подавление: псайкер на полминуты лишён сил, его напряжение падает до нуля — варп отрезан.</summary>
    private void Suppress(EntityUid user, EntityUid target)
    {
        if (!TryComp<CadiaPsykerComponent>(target, out var other))
        {
            _popup.PopupEntity(Loc.GetString("cadia-psy-not-psyker"), user, user);
            return;
        }
        other.SilencedUntil = _timing.CurTime + SilencedDelay;
        other.Strain = 0f;
        Dirty(target, other);
        _popup.PopupEntity(Loc.GetString("cadia-psy-suppressed"), target, target, PopupType.LargeCaution);
        _audio.PlayPvs(SilencedSound, target);
    }

    /// <summary>Ярость Древних: огненный зверь идёт по прямой до точки и бьёт всех рядом с путём.</summary>
    private void Fury(EntityUid user, MapCoordinates target)
    {
        var from = _xform.GetMapCoordinates(user);
        if (from.MapId != target.MapId)
            return;
        var delta = target.Position - from.Position;
        var length = Math.Min(delta.Length(), 10f);
        if (length < 0.5f)
            return;
        var dir = delta.Normalized();
        var hit = new HashSet<EntityUid>();
        var faction = CompOrNull<CadiaFactionMemberComponent>(user)?.Faction;
        for (var d = 1f; d <= length; d += 1f)
        {
            var point = from.Offset(dir * d);
            Spawn(CadiaPsyFuryEffect, point);
            _mobs.Clear();
            _lookup.GetEntitiesInRange(point, FuryRange, _mobs);
            foreach (var mob in _mobs)
            {
                if (mob.Owner == user || !hit.Add(mob))
                    continue;
                if (faction != null && TryComp<CadiaFactionMemberComponent>(mob, out var other) && other.Faction == faction)
                    continue;
                Hurt(mob, user, "Heat", FuryAmount);
            }
        }
        _audio.PlayPvs(FireSound, user);
    }

    /// <summary>Огонь варпа Тзинча: разноцветное пламя в точке, бьёт и поджигает всё вокруг.</summary>
    private void Warpfire(EntityUid user, MapCoordinates target)
    {
        Spawn(CadiaPsyWarpfireEffect, target);
        _mobs.Clear();
        _lookup.GetEntitiesInRange(target, WarpfireRange, _mobs);
        var faction = CompOrNull<CadiaFactionMemberComponent>(user)?.Faction;
        foreach (var mob in _mobs)
        {
            if (mob.Owner == user)
                continue;
            if (faction != null && TryComp<CadiaFactionMemberComponent>(mob, out var other) && other.Faction == faction)
                continue;
            Hurt(mob, user, "Heat", WarpfireAmount);
            Hurt(mob, user, "Cellular", WarpfireAmount2);
        }
        _audio.PlayPvs(FireSound, user);
    }

    /// <summary>Изменение облика: 15 секунд почти невидим — сложная смена облика заменена завесой Тзинча.</summary>
    private void Veil(EntityUid user)
    {
        var stealth = EnsureComp<StealthComponent>(user);
        _stealth.SetVisibility(user, -0.7f, stealth);
        EnsureComp<CadiaVeilComponent>(user).Until = _timing.CurTime + VeilDelay;
        _audio.PlayPvs(VeilSound, user);
    }

    /// <summary>Мор Нургла: облако язв вокруг псайкера.</summary>
    private void Plague(EntityUid user)
    {
        foreach (var enemy in Enemies(user, PlagueRadius))
        {
            Hurt(enemy, user, "Poison", PlagueAmount);
            Hurt(enemy, user, "Cellular", PlagueAmount2);
        }
        Spawn(CadiaPsyPlagueEffect, _xform.GetMapCoordinates(user));
        _audio.PlayPvs(PlagueSound, user);
    }

    /// <summary>Очарование Слаанеш: враги рядом оглушены и теряют волю к бою.</summary>
    private void Charm(EntityUid user)
    {
        foreach (var enemy in Enemies(user, CharmRadius))
            _stun.TryUpdateParalyzeDuration(enemy, CharmDuration);
        Spawn(CadiaPsyRingPink, _xform.GetMapCoordinates(user));
        _audio.PlayPvs(ScreamSound, user);
    }
}
