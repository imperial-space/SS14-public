using System.Linq;
using System.Numerics;
using Content.Server.Emp;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Throwing;
using Content.Shared.Temperature.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard.Spells;

#region Компоненты

/// <summary>aoe/magic_missile: медленные самонаводящиеся ракеты во всех вокруг.</summary>
[RegisterComponent]
public sealed partial class ImperialMagicMissileSpellComponent : Component
{
    [DataField]
    public EntProtoId Projectile = "ImperialProjectileMagicMissile";

    [DataField]
    public float Radius = 7f;

    /// <summary>max_targets (0 — без ограничения).</summary>
    [DataField]
    public int MaxTargets;
}

/// <summary>Снаряд, который роняет цель (paralyze у волшебной ракеты).</summary>
[RegisterComponent]
public sealed partial class ImperialParalyzeProjectileComponent : Component
{
    [DataField]
    public TimeSpan Time = TimeSpan.FromSeconds(6);
}

/// <summary>emp/disable_tech: ЭМИ вокруг мага.</summary>
[RegisterComponent]
public sealed partial class ImperialEmpSpellComponent : Component
{
    [DataField]
    public float Range = 10f;

    [DataField]
    public float Energy = 50000f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(60);
}

/// <summary>aoe/repulse: отбросить всё вокруг.</summary>
[RegisterComponent]
public sealed partial class ImperialRepulseSpellComponent : Component
{
    [DataField]
    public float Radius = 5f;

    /// <summary>max_throw.</summary>
    [DataField]
    public float MaxThrow = 5f;
}

/// <summary>Мешочек заклинаний: брошенный, бьёт током (spellpacket).</summary>
[RegisterComponent]
public sealed partial class ImperialSpellPacketComponent : Component
{
    [DataField]
    public int Shock = 80;
}

/// <summary>timestop: поле остановки времени (радиус 2, 10 секунд).</summary>
[RegisterComponent]
public sealed partial class ImperialTimestopSpellComponent : Component
{
    [DataField]
    public EntProtoId Field = "ImperialTimestopField";

    [DataField]
    public float Range = 2f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(10);
}

/// <summary>Поле остановки времени: всё внутри, кроме неуязвимых, замирает.</summary>
[RegisterComponent]
public sealed partial class ImperialTimestopFieldComponent : Component
{
    [ViewVariables]
    public float Range = 2f;

    [ViewVariables]
    public TimeSpan End;

    [ViewVariables]
    public HashSet<EntityUid> Immune = new();

    /// <summary>Замороженные снаряды и их скорости.</summary>
    [ViewVariables]
    public Dictionary<EntityUid, Vector2> Projectiles = new();

    [ViewVariables]
    public HashSet<EntityUid> Mobs = new();
}

/// <summary>Неуязвим к остановке времени (TRAIT_TIME_STOP_IMMUNE у владельца заклинания).</summary>
[RegisterComponent]
public sealed partial class ImperialTimestopImmuneComponent : Component;

/// <summary>forcewall: три клетки стены, через которые проходит только маг.</summary>
[RegisterComponent]
public sealed partial class ImperialForcewallSpellComponent : Component
{
    [DataField]
    public EntProtoId Wall = "ImperialForcewall";
}

/// <summary>Стена мага: пропускает заклинателя и тех, кто под антимагией.</summary>
[RegisterComponent]
public sealed partial class ImperialForcewallComponent : Component
{
    [ViewVariables]
    public EntityUid? Caster;
}

/// <summary>lichdom: душа привязывается к предмету в руке, маг становится личом.</summary>
[RegisterComponent]
public sealed partial class ImperialLichdomSpellComponent : Component;

/// <summary>/datum/component/phylactery: пока предмет цел, лич возрождается.</summary>
[RegisterComponent]
public sealed partial class ImperialPhylacteryComponent : Component
{
    [ViewVariables]
    public EntityUid Mind;

    /// <summary>base_respawn_time.</summary>
    [DataField]
    public TimeSpan RespawnTime = TimeSpan.FromMinutes(3);

    /// <summary>stun_per_resurrection.</summary>
    [DataField]
    public TimeSpan StunPerResurrection = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public int Resurrections;

    [ViewVariables]
    public TimeSpan? ReviveAt;

    [ViewVariables]
    public EntityUid? Corpse;
}

/// <summary>Лич: тело без души (TRAIT_NO_SOUL).</summary>
[RegisterComponent]
public sealed partial class ImperialLichComponent : Component;

/// <summary>chuuni_invocations: все заклинания выкрикиваются по-дурацки, после каста — немного лечения.</summary>
[RegisterComponent]
public sealed partial class ImperialChuuniSpellComponent : Component;

/// <summary>/datum/component/chuunibyou.</summary>
[RegisterComponent]
public sealed partial class ImperialChuunibyouComponent : Component
{
    [DataField]
    public float Heal = 3f;

    [ViewVariables]
    public TimeSpan NextHeal;
}

/// <summary>spacetime_dist: пары искажённых клеток в радиусе 7 на 15 секунд.</summary>
[RegisterComponent]
public sealed partial class ImperialSpacetimeSpellComponent : Component
{
    [DataField]
    public float Radius = 7f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(15);

    [DataField]
    public EntProtoId Effect = "ImperialSpacetimeDistortion";

    [ViewVariables]
    public bool Ready = true;

    [ViewVariables]
    public TimeSpan ReadyAt;
}

/// <summary>Искажение пространства-времени: шагнувший переносится на парную клетку.</summary>
[RegisterComponent]
public sealed partial class ImperialSpacetimeDistortionComponent : Component
{
    [ViewVariables]
    public EntityUid? Linked;

    /// <summary>walks_left.</summary>
    [ViewVariables]
    public int WalksLeft = 50;

    [ViewVariables]
    public TimeSpan Busy;
}

/// <summary>Ловушка (/obj/structure/trap): срабатывает от шага, маг-создатель неуязвим.</summary>
[RegisterComponent]
public sealed partial class ImperialWizardTrapComponent : Component
{
    [DataField]
    public ImperialTrapKind Kind;

    /// <summary>charges у ловушек заклинания — 1.</summary>
    [DataField]
    public int Charges = 1;

    [ViewVariables]
    public HashSet<EntityUid> ImmuneMinds = new();
}

public enum ImperialTrapKind : byte
{
    Stun,
    Fire,
    Chill,
    Damage,
}

/// <summary>Ловушки заклинания «Ловушки!» запоминают разум мага.</summary>
[RegisterComponent]
public sealed partial class ImperialTrapsSpellComponent : Component;

/// <summary>touch/duffelbag: проклятый вещмешок на спину цели.</summary>
[RegisterComponent]
public sealed partial class ImperialDuffelbagSpellComponent : Component
{
    [DataField]
    public EntProtoId Bag = "ImperialCursedDuffelbag";
}

/// <summary>/datum/component/curse_of_hunger: голодный мешок кусает носителя, если его не кормить.</summary>
[RegisterComponent]
public sealed partial class ImperialCurseOfHungerComponent : Component
{
    [ViewVariables]
    public float Hunger;

    [ViewVariables]
    public TimeSpan NextTick;

    /// <summary>Сколько раз покормили (после пяти кормлений проклятие спадает).</summary>
    [ViewVariables]
    public int Fed;
}

#endregion

public sealed partial class ImperialWizardSpellsSystem
{
    [Dependency] private readonly EmpSystem _emp = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly Content.Server.Temperature.Systems.TemperatureSystem _temperature = default!;
    [Dependency] private readonly Content.Shared.Atmos.EntitySystems.SharedAtmosphereSystem _atmos = default!;

    private static readonly SoundSpecifier TimeSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/timeparadox2.ogg");

    /// <summary>chunibyou_invocations по школам магии.</summary>
    private static readonly Dictionary<string, string> ChuuniSchools = new()
    {
        ["unset"] = "imperial-chuuni-unset",
        ["holy"] = "imperial-chuuni-holy",
        ["psychic"] = "imperial-chuuni-psychic",
        ["mime"] = "imperial-chuuni-mime",
        ["restoration"] = "imperial-chuuni-restoration",
        ["evocation"] = "imperial-chuuni-evocation",
        ["transmutation"] = "imperial-chuuni-transmutation",
        ["translocation"] = "imperial-chuuni-translocation",
        ["conjuration"] = "imperial-chuuni-conjuration",
        ["necromancy"] = "imperial-chuuni-necromancy",
        ["forbidden"] = "imperial-chuuni-forbidden",
        ["sanguine"] = "imperial-chuuni-sanguine",
    };

    private void InitializeDefensive()
    {
        SubscribeLocalEvent<ImperialMagicMissileSpellComponent, ImperialSpellEffectEvent>(OnMagicMissile);
        SubscribeLocalEvent<ImperialParalyzeProjectileComponent, ProjectileHitEvent>(OnParalyzeHit);
        SubscribeLocalEvent<ImperialEmpSpellComponent, ImperialSpellEffectEvent>(OnEmpSpell);
        SubscribeLocalEvent<ImperialRepulseSpellComponent, ImperialSpellEffectEvent>(OnRepulse);
        SubscribeLocalEvent<ImperialSpellPacketComponent, ThrowDoHitEvent>(OnPacketHit);
        SubscribeLocalEvent<ImperialSpellPacketComponent, ThrownEvent>(OnPacketThrown);
        SubscribeLocalEvent<ImperialTimestopSpellComponent, ImperialSpellEffectEvent>(OnTimestop);
        SubscribeLocalEvent<ImperialForcewallSpellComponent, ImperialSpellEffectEvent>(OnForcewall);
        SubscribeLocalEvent<ImperialForcewallComponent, PreventCollideEvent>(OnForcewallCollide);
        SubscribeLocalEvent<ImperialLichdomSpellComponent, ImperialSpellEffectEvent>(OnLichdom);
        SubscribeLocalEvent<ImperialLichdomSpellComponent, ImperialSpellChannelDoAfterEvent>(OnLichdomDone);
        SubscribeLocalEvent<ImperialPhylacteryComponent, Content.Shared.Examine.ExaminedEvent>(OnPhylacteryExamine);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ImperialChuuniSpellComponent, ImperialSpellEffectEvent>(OnChuuni);
        SubscribeLocalEvent<ImperialChuuniSpellComponent, ImperialSpellChannelDoAfterEvent>(OnChuuniDone);
        SubscribeLocalEvent<ImperialChuunibyouComponent, ImperialSpellInvocationEvent>(OnChuuniInvocation);
        SubscribeLocalEvent<ImperialChuunibyouComponent, ImperialSpellCastEvent>(OnChuuniCast);
        SubscribeLocalEvent<ImperialSpacetimeSpellComponent, ImperialSpellEffectEvent>(OnSpacetime);
        SubscribeLocalEvent<ImperialSpacetimeDistortionComponent, StepTriggeredOffEvent>(OnDistortionStep);
        SubscribeLocalEvent<ImperialSpacetimeDistortionComponent, StepTriggerAttemptEvent>(OnStepAttempt);
        SubscribeLocalEvent<ImperialWizardTrapComponent, StepTriggeredOffEvent>(OnTrapStep);
        SubscribeLocalEvent<ImperialWizardTrapComponent, StepTriggerAttemptEvent>(OnTrapStepAttempt);
        SubscribeLocalEvent<ImperialTrapsSpellComponent, ImperialSpellEffectEvent>(OnTraps);
        SubscribeLocalEvent<ImperialDuffelbagSpellComponent, ImperialTouchHitEvent>(OnDuffelbag);
        SubscribeLocalEvent<ImperialCurseOfHungerComponent, Content.Shared.Interaction.InteractUsingEvent>(OnFeedBag);
    }

    #region Ракеты, ЭМИ, отталкивание

    /// <summary>magic_missile: по ракете в каждое существо в поле зрения.</summary>
    private void OnMagicMissile(Entity<ImperialMagicMissileSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        args.Handled = true;
        var caster = args.Caster;
        var from = _transform.GetMapCoordinates(caster);
        var targets = _lookup.GetEntitiesInRange<MobStateComponent>(from, ent.Comp.Radius)
            .Where(m => m.Owner != caster && !_mobs.IsDead(m))
            .Select(m => m.Owner)
            .ToList();
        if (ent.Comp.MaxTargets > 0)
        {
            _random.Shuffle(targets);
            targets = targets.Take(ent.Comp.MaxTargets).ToList();
        }

        foreach (var target in targets)
        {
            var direction = _transform.GetWorldPosition(target) - from.Position;
            var missile = Spawn(ent.Comp.Projectile, from);
            _guns.ShootProjectile(missile, direction.Normalized(), Vector2.Zero, ent, caster, 4f);
            var homing = EnsureComp<ImperialHomingProjectileComponent>(missile);
            homing.Target = target;
            homing.TurnRate = 360f;
        }
    }

    private void OnParalyzeHit(Entity<ImperialParalyzeProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (_antimagic.CanBlockMagic(args.Target))
        {
            args.Damage = new DamageSpecifier();
            return;
        }

        _stun.TryUpdateParalyzeDuration(args.Target, ent.Comp.Time);
    }

    /// <summary>disable_tech: empulse(heavy 6, light 10).</summary>
    private void OnEmpSpell(Entity<ImperialEmpSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        args.Handled = true;
        _emp.EmpPulse(_transform.GetMapCoordinates(args.Caster), ent.Comp.Range, ent.Comp.Energy, ent.Comp.Duration, args.Caster);
    }

    /// <summary>repulse: всё незакреплённое вокруг отлетает, существа падают.</summary>
    private void OnRepulse(Entity<ImperialRepulseSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        args.Handled = true;
        var caster = args.Caster;
        var center = _transform.GetWorldPosition(caster);
        foreach (var victim in _lookup.GetEntitiesInRange(Transform(caster).Coordinates, ent.Comp.Radius, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (victim == caster || Transform(victim).Anchored || !TryComp<PhysicsComponent>(victim, out _))
                continue;

            if (HasComp<MobStateComponent>(victim) && _antimagic.CanBlockMagic(victim))
                continue;

            var offset = _transform.GetWorldPosition(victim) - center;
            var distance = offset.Length();
            if (distance < 0.5f)
            {
                if (HasComp<MobStateComponent>(victim))
                {
                    _stun.TryUpdateParalyzeDuration(victim, TimeSpan.FromSeconds(10));
                    _damage.TryChangeDamage(victim, new DamageSpecifier { DamageDict = { ["Blunt"] = 5 } }, origin: caster);
                    _popup.PopupEntity(Loc.GetString("imperial-spell-repulse-slammed", ("caster", caster)), victim, victim, PopupType.LargeCaution);
                }

                continue;
            }

            if (HasComp<MobStateComponent>(victim))
            {
                _stun.TryUpdateParalyzeDuration(victim, TimeSpan.FromSeconds(4));
                _popup.PopupEntity(Loc.GetString("imperial-spell-repulse-thrown", ("caster", caster)), victim, victim, PopupType.LargeCaution);
            }

            Spawn("EffectGravityPulse", Transform(victim).Coordinates);
            var range = Math.Clamp(ent.Comp.MaxThrow - Math.Clamp(distance - 2, 0, distance), 3, ent.Comp.MaxThrow);
            _throwing.TryThrow(victim, offset.Normalized() * range, 10f, caster, recoil: false);
        }
    }

    /// <summary>spellpacket: попадание — удар током 80, мешочек исчезает.</summary>
    private void OnPacketHit(Entity<ImperialSpellPacketComponent> ent, ref ThrowDoHitEvent args)
    {
        if (HasComp<MobStateComponent>(args.Target) && !_antimagic.CanBlockMagic(args.Target))
            _electrocution.TryDoElectrocution(args.Target, ent, ent.Comp.Shock, TimeSpan.FromSeconds(4), true, ignoreInsulation: true);
        QueueDel(ent);
    }

    private void OnPacketThrown(Entity<ImperialSpellPacketComponent> ent, ref ThrownEvent args)
    {
        if (args.User is { } thrower && HasComp<HumanoidProfileComponent>(thrower))
            _chat.TrySendInGameICMessage(thrower, Loc.GetString("imperial-spell-packet-shout"), Content.Shared.Chat.InGameICChatType.Speak, false, ignoreActionBlocker: true);
    }

    #endregion

    #region Остановка времени

    /// <summary>timestop: поле вокруг мага, маг неуязвим.</summary>
    private void OnTimestop(Entity<ImperialTimestopSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        args.Handled = true;
        var field = Spawn(ent.Comp.Field, _transform.GetMapCoordinates(args.Caster));
        var comp = EnsureComp<ImperialTimestopFieldComponent>(field);
        comp.Range = ent.Comp.Range;
        comp.End = _timing.CurTime + ent.Comp.Duration;
        comp.Immune.Add(args.Caster);
        var immune = EntityQueryEnumerator<ImperialTimestopImmuneComponent>();
        while (immune.MoveNext(out var uid, out _))
        {
            comp.Immune.Add(uid);
        }

        _audio.PlayPvs(TimeSound, field);
    }

    /// <summary>chronofield: замораживать новых в поле, отпускать по окончании.</summary>
    private void UpdateTimestop()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialTimestopFieldComponent, TransformComponent>();
        var ended = new List<Entity<ImperialTimestopFieldComponent>>();
        while (query.MoveNext(out var uid, out var field, out var xform))
        {
            if (now >= field.End)
            {
                ended.Add((uid, field));
                continue;
            }

            foreach (var other in _lookup.GetEntitiesInRange(xform.Coordinates, field.Range, LookupFlags.Dynamic | LookupFlags.Sundries))
            {
                if (field.Immune.Contains(other) || other == uid)
                    continue;

                if (HasComp<MobStateComponent>(other))
                {
                    if (_antimagic.CanBlockMagic(other, effect: false))
                        continue;

                    field.Mobs.Add(other);
                    _stun.TryUpdateStunDuration(other, TimeSpan.FromSeconds(1));
                    continue;
                }

                if (HasComp<ProjectileComponent>(other) && TryComp<PhysicsComponent>(other, out var physics))
                {
                    if (!field.Projectiles.ContainsKey(other))
                        field.Projectiles[other] = physics.LinearVelocity;
                    _physics.SetLinearVelocity(other, Vector2.Zero, body: physics);
                    if (TryComp<Robust.Shared.Spawners.TimedDespawnComponent>(other, out var despawn))
                        despawn.Lifetime += 0.1f;
                }
            }
        }

        foreach (var field in ended)
        {
            foreach (var (projectile, velocity) in field.Comp.Projectiles)
            {
                if (!TerminatingOrDeleted(projectile))
                    _physics.SetLinearVelocity(projectile, velocity);
            }

            _audio.PlayPvs(TimeSound, field);
            QueueDel(field);
        }
    }

    #endregion

    #region Стена силы

    /// <summary>forcewall: стена на клетке мага и по бокам, 30 секунд.</summary>
    private void OnForcewall(Entity<ImperialForcewallSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        args.Handled = true;
        var xform = Transform(args.Caster);
        var dir = _transform.GetWorldRotation(xform).GetCardinalDir().ToVec();
        var side = new Vector2(-dir.Y, dir.X);
        foreach (var offset in new[] { Vector2.Zero, side, -side })
        {
            var wall = Spawn(ent.Comp.Wall, _transform.GetMapCoordinates(args.Caster).Offset(offset));
            _transform.AnchorEntity(wall);
            EnsureComp<ImperialForcewallComponent>(wall).Caster = args.Caster;
        }
    }

    private void OnForcewallCollide(Entity<ImperialForcewallComponent> ent, ref PreventCollideEvent args)
    {
        if (args.OtherEntity == ent.Comp.Caster
            || HasComp<MobStateComponent>(args.OtherEntity) && _antimagic.CanBlockMagic(args.OtherEntity, effect: false))
            args.Cancelled = true;
    }

    #endregion

    #region Лич

    /// <summary>lichdom: 5 секунд сосредоточения на предмете в руке.</summary>
    private void OnLichdom(Entity<ImperialLichdomSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        var caster = args.Caster;
        if (HasComp<ImperialLichComponent>(caster))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-lich-no-soul"), caster, caster);
            return;
        }

        if (!_hands.TryGetActiveItem(caster, out var item) || HasComp<Content.Shared.Interaction.Components.UnremoveableComponent>(item))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-lich-no-item"), caster, caster);
            return;
        }

        args.Handled = true;
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/pope_entry.ogg"), caster);
        _popup.PopupEntity(Loc.GetString("imperial-spell-lich-focus", ("item", item.Value)), caster, caster);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, caster, TimeSpan.FromSeconds(5), new ImperialSpellChannelDoAfterEvent(), ent, item.Value)
        {
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnLichdomDone(Entity<ImperialLichdomSpellComponent> ent, ref ImperialSpellChannelDoAfterEvent args)
    {
        var caster = args.User;
        if (args.Cancelled || args.Target is not { } item || !_mind.TryGetMind(caster, out var mindId, out _))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-lich-broken"), caster, caster);
            return;
        }

        var phylactery = EnsureComp<ImperialPhylacteryComponent>(item);
        phylactery.Mind = mindId;
        _meta.SetEntityName(item, Loc.GetString("imperial-spell-lich-ensouled", ("item", Name(item))));
        MakeLich(Skeletonize(caster));
        _popup.PopupEntity(Loc.GetString("imperial-spell-lich-done"), caster, caster, PopupType.LargeCaution);
        Log.Info($"{ToPrettyString(caster)} стал личом, филактерия {ToPrettyString(item)}");
        _actions.RemoveAction(ent.Owner);
        QueueDel(ent);
    }

    /// <summary>Всё снаряжение и вещи из рук — на пол (drop_items).</summary>
    public void DropEverything(EntityUid body)
    {
        if (_inventory.TryGetSlots(body, out var slots))
        {
            foreach (var slot in slots)
                _inventory.TryUnequip(body, slot.Name, true, true);
        }

        foreach (var held in _hands.EnumerateHeld(body).ToList())
            _hands.TryDrop(body, held, checkActionBlocker: false);
    }

    /// <summary>set_species(skeleton): разум и всё снаряжение переходят в тело-скелет.</summary>
    public EntityUid Skeletonize(EntityUid body)
    {
        var coords = Transform(body).Coordinates;
        var skeleton = Spawn("MobSkeletonPerson", coords);
        if (_inventory.TryGetSlots(body, out var slots))
        {
            foreach (var slot in slots)
            {
                if (!_inventory.TryGetSlotEntity(body, slot.Name, out var item) || !_inventory.TryUnequip(body, slot.Name, true, true))
                    continue;

                _inventory.TryUnequip(skeleton, slot.Name, true, true);
                _inventory.TryEquip(skeleton, item.Value, slot.Name, true, true);
            }
        }

        foreach (var held in _hands.EnumerateHeld(body).ToList())
        {
            _hands.TryDrop(body, held, checkActionBlocker: false);
            _hands.TryPickupAnyHand(skeleton, held);
        }

        _meta.SetEntityName(skeleton, Name(body));
        if (TryComp<Content.Shared.NPC.Components.NpcFactionMemberComponent>(body, out var factions))
        {
            _faction.ClearFactions(skeleton);
            foreach (var faction in factions.Factions)
            {
                _faction.AddFaction(skeleton, faction);
            }
        }

        if (HasComp<ImperialMagicallyGiftedComponent>(body))
            EnsureComp<ImperialMagicallyGiftedComponent>(skeleton);
        if (_mind.TryGetMind(body, out var mindId, out var mind))
            _mind.TransferTo(mindId, skeleton, mind: mind);
        QueueDel(body);
        return skeleton;
    }

    /// <summary>set_species(skeleton) и чёрная мантия.</summary>
    private void MakeLich(EntityUid body)
    {
        EnsureComp<ImperialLichComponent>(body);

        foreach (var (slot, proto) in new[] { ("outerClothing", "ImperialClothingOuterWizardBlack"), ("head", "ImperialClothingHeadHatWizardBlack"), ("jumpsuit", "ClothingUniformJumpsuitColorBlack") })
        {
            _inventory.TryUnequip(body, slot, true, true);
            var item = Spawn(proto, Transform(body).Coordinates);
            if (!_inventory.TryEquip(body, item, slot, true, true))
                QueueDel(item);
        }
    }

    private void OnPhylacteryExamine(Entity<ImperialPhylacteryComponent> ent, ref Content.Shared.Examine.ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(_mind.TryGetMind(args.Examiner, out var mind, out _) && mind == ent.Comp.Mind
            ? "imperial-spell-lich-examine-own"
            : "imperial-spell-lich-examine"));
        if (ent.Comp.Resurrections > 0)
            args.PushMarkup(Loc.GetString("imperial-spell-lich-notches", ("count", ent.Comp.Resurrections)));
    }

    /// <summary>check_if_lich_died(): через 3 минуты лич возродится у филактерии.</summary>
    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || !_mind.TryGetMind(args.Target, out var mindId, out _))
            return;

        var query = EntityQueryEnumerator<ImperialPhylacteryComponent>();
        while (query.MoveNext(out var uid, out var phylactery))
        {
            if (phylactery.Mind != mindId || phylactery.ReviveAt != null)
                continue;

            phylactery.ReviveAt = _timing.CurTime + phylactery.RespawnTime;
            phylactery.Corpse = args.Target;
            _popup.PopupEntity(Loc.GetString("imperial-spell-lich-dragged", ("minutes", phylactery.RespawnTime.TotalMinutes)), args.Target, args.Target, PopupType.Large);
        }
    }


    private bool IsLichAlive(MindComponent mind)
    {
        return mind.OwnedEntity is { } body && HasComp<MobStateComponent>(body) && !_mobs.IsDead(body);
    }
    private void UpdatePhylacteries()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialPhylacteryComponent, TransformComponent>();
        var revive = new List<Entity<ImperialPhylacteryComponent>>();
        while (query.MoveNext(out var uid, out var phylactery, out _))
        {
            // Тело уничтожили без смерти (гиб, удаление) — таймер тоже запускается.
            if (phylactery.ReviveAt == null && phylactery.Mind.Valid && TryComp<MindComponent>(phylactery.Mind, out var lichMind) && !IsLichAlive(lichMind)
                && (lichMind.OwnedEntity is not { } owned || !HasComp<MobStateComponent>(owned)))
            {
                phylactery.ReviveAt = now + phylactery.RespawnTime;
                if (lichMind.OwnedEntity is { } ghost)
                    _popup.PopupEntity(Loc.GetString("imperial-spell-lich-dragged", ("minutes", phylactery.RespawnTime.TotalMinutes)), ghost, ghost, PopupType.Large);
            }

            if (phylactery.ReviveAt is { } at && now >= at)
                revive.Add((uid, phylactery));
        }

        foreach (var phylactery in revive)
        {
            ReviveLich(phylactery);
        }
    }

    /// <summary>revive_lich(): новое тело-скелет у филактерии, разум переходит, оглушение растёт с каждым разом.</summary>
    private void ReviveLich(Entity<ImperialPhylacteryComponent> phylactery)
    {
        phylactery.Comp.ReviveAt = null;
        if (!TryComp<MindComponent>(phylactery.Comp.Mind, out var mind))
            return;

        // После смерти разум уходит призраку — «живым» считается только тело-моб.
        if (IsLichAlive(mind))
            return;

        var coords = _transform.GetMapCoordinates(phylactery);
        var lich = Spawn("MobSkeletonPerson", coords);
        foreach (var (slot, proto) in new[] { ("shoes", "ClothingShoesWizard"), ("jumpsuit", "ClothingUniformJumpsuitColorBlack"), ("outerClothing", "ImperialClothingOuterWizardBlack"), ("head", "ImperialClothingHeadHatWizardBlack") })
        {
            var item = Spawn(proto, coords);
            if (!_inventory.TryEquip(lich, item, slot, true, true))
                QueueDel(item);
        }

        EnsureComp<ImperialLichComponent>(lich);
        _meta.SetEntityName(lich, mind.CharacterName ?? Name(lich));
        _mind.TransferTo(phylactery.Comp.Mind, lich, mind: mind);
        phylactery.Comp.Resurrections++;
        _stun.TryUpdateParalyzeDuration(lich, phylactery.Comp.StunPerResurrection * phylactery.Comp.Resurrections);
        _popup.PopupEntity(Loc.GetString("imperial-spell-lich-revived"), lich, lich, PopupType.Large);

        // Старое тело рассыпается в прах, вещи падают на пол (dust(drop_items = TRUE)).
        if (phylactery.Comp.Corpse is { } corpse && !TerminatingOrDeleted(corpse))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-lich-corpse", ("corpse", corpse)), corpse, PopupType.MediumCaution);
            if (_inventory.TryGetSlots(corpse, out var corpseSlots))
            {
                foreach (var slot in corpseSlots)
                    _inventory.TryUnequip(corpse, slot.Name, true, true);
            }

            foreach (var held in _hands.EnumerateHeld(corpse).ToList())
                _hands.TryDrop(corpse, held, checkActionBlocker: false);

            Spawn("Ash", Transform(corpse).Coordinates);
            QueueDel(corpse);
        }

        phylactery.Comp.Corpse = null;
    }

    #endregion

    #region Чуни

    private void OnChuuni(Entity<ImperialChuuniSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("imperial-spell-chuuni-focus"), args.Caster, args.Caster);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.Caster, TimeSpan.FromSeconds(5), new ImperialSpellChannelDoAfterEvent(), ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        });
    }

    private void OnChuuniDone(Entity<ImperialChuuniSpellComponent> ent, ref ImperialSpellChannelDoAfterEvent args)
    {
        var caster = args.User;
        if (args.Cancelled)
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-chuuni-broken"), caster, caster);
            return;
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/bamf.ogg"), caster);
        _popup.PopupEntity(Loc.GetString("imperial-spell-chuuni-done"), caster, caster, PopupType.Large);
        EnsureComp<ImperialChuunibyouComponent>(caster);

        // Шляпа превращается в повязку на глаз.
        _inventory.TryUnequip(caster, "eyes", true, true);
        if (_inventory.TryGetSlotEntity(caster, "head", out var hat) && HasComp<Content.Shared.Magic.Components.WizardClothesComponent>(hat))
            QueueDel(hat.Value);
        var patch = Spawn("ImperialClothingEyesChuuniPatch", Transform(caster).Coordinates);
        if (!_inventory.TryEquip(caster, patch, "eyes", true, true))
            QueueDel(patch);

        _actions.RemoveAction(ent.Owner);
        QueueDel(ent);
    }

    /// <summary>on_pre_invocation(): всегда крик, без искажений, фраза школы.</summary>
    private void OnChuuniInvocation(Entity<ImperialChuunibyouComponent> ent, ref ImperialSpellInvocationEvent args)
    {
        var school = TryComp<ImperialSpellComponent>(args.Spell, out var spell) ? spell.School : "unset";
        if (!ChuuniSchools.TryGetValue(school, out var loc))
            loc = ChuuniSchools["unset"];

        args.Type = ImperialInvocationType.Shout;
        args.Message = Loc.GetString(loc);
        if (spell != null)
            spell.GarbledProb = 0f;
    }

    /// <summary>on_after_spell_cast(): +3 здоровья раз в 5 секунд.</summary>
    private void OnChuuniCast(Entity<ImperialChuunibyouComponent> ent, ref ImperialSpellCastEvent args)
    {
        if (_timing.CurTime < ent.Comp.NextHeal)
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-chuuni-cooldown"), ent, ent);
            return;
        }

        ent.Comp.NextHeal = _timing.CurTime + TimeSpan.FromSeconds(5);
        _damage.TryChangeDamage(ent.Owner, new DamageSpecifier { DamageDict = { ["Blunt"] = -ent.Comp.Heal / 2, ["Heat"] = -ent.Comp.Heal / 2 } }, true);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/staff_healing.ogg"), ent);
        _popup.PopupEntity(Loc.GetString("imperial-spell-chuuni-heal"), ent, ent);
    }

    #endregion

    #region Искажение пространства-времени

    /// <summary>spacetime_dist: половина клеток в радиусе 7 связывается попарно.</summary>
    private void OnSpacetime(Entity<ImperialSpacetimeSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        if (!ent.Comp.Ready)
            return;

        var tiles = FreeTilesAround(Transform(args.Caster).Coordinates, ent.Comp.Radius, true);
        if (tiles.Count < 2)
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-spacetime-fail"), args.Caster, args.Caster);
            return;
        }

        args.Handled = true;
        ent.Comp.Ready = false;
        ent.Comp.ReadyAt = _timing.CurTime + ent.Comp.Duration;
        LinkDistortions(tiles, ent.Comp.Effect, ent.Comp.Duration);
    }

    /// <summary>spacetime_dist/cast(): искажения вокруг точки (и для Великого ритуала).</summary>
    public void CreateSpacetime(EntityCoordinates center, float radius, TimeSpan duration)
    {
        LinkDistortions(FreeTilesAround(center, radius, true), "ImperialSpacetimeDistortion", duration);
    }

    private void LinkDistortions(List<EntityCoordinates> tiles, EntProtoId effect, TimeSpan duration)
    {
        var pairs = tiles.Count / 2;
        for (var i = 0; i < pairs && tiles.Count >= 2; i++)
        {
            var a = Spawn(effect, _random.PickAndTake(tiles));
            var b = Spawn(effect, _random.PickAndTake(tiles));
            EnsureComp<ImperialSpacetimeDistortionComponent>(a).Linked = b;
            EnsureComp<ImperialSpacetimeDistortionComponent>(b).Linked = a;
            EnsureComp<Robust.Shared.Spawners.TimedDespawnComponent>(a).Lifetime = (float) duration.TotalSeconds;
            EnsureComp<Robust.Shared.Spawners.TimedDespawnComponent>(b).Lifetime = (float) duration.TotalSeconds;
        }
    }

    private void OnStepAttempt(Entity<ImperialSpacetimeDistortionComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = _timing.CurTime >= ent.Comp.Busy;
    }

    /// <summary>walk_link(): перенести на парную клетку.</summary>
    private void OnDistortionStep(Entity<ImperialSpacetimeDistortionComponent> ent, ref StepTriggeredOffEvent args)
    {
        if (ent.Comp.Linked is not { } linked || TerminatingOrDeleted(linked) || ent.Comp.WalksLeft <= 0
            || !TryComp<ImperialSpacetimeDistortionComponent>(linked, out var other))
            return;

        if (HasComp<MobStateComponent>(args.Tripper) && _antimagic.CanBlockMagic(args.Tripper, effect: false))
            return;

        ent.Comp.WalksLeft--;
        other.Busy = _timing.CurTime + TimeSpan.FromSeconds(0.5);
        _transform.SetCoordinates(args.Tripper, Transform(linked).Coordinates);
    }

    private void UpdateSpacetime()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialSpacetimeSpellComponent>();
        while (query.MoveNext(out _, out var spell))
        {
            if (!spell.Ready && now >= spell.ReadyAt)
                spell.Ready = true;
        }
    }

    #endregion

    #region Ловушки

    /// <summary>the_traps: создатель запоминается в immune_minds (после призыва).</summary>
    private void OnTraps(Entity<ImperialTrapsSpellComponent> ent, ref ImperialSpellEffectEvent args)
    {
        if (!_mind.TryGetMind(args.Caster, out var mindId, out _))
            return;

        foreach (var trap in _lookup.GetEntitiesInRange<ImperialWizardTrapComponent>(Transform(args.Caster).Coordinates, 4f))
        {
            trap.Comp.ImmuneMinds.Add(mindId);
        }
    }

    private void OnTrapStepAttempt(Entity<ImperialWizardTrapComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = HasComp<MobStateComponent>(args.Tripper)
                        && !(_mind.TryGetMind(args.Tripper, out var mind, out _) && ent.Comp.ImmuneMinds.Contains(mind));
    }

    /// <summary>trap_effect(): шок, огонь, холод или землетрясение.</summary>
    private void OnTrapStep(Entity<ImperialWizardTrapComponent> ent, ref StepTriggeredOffEvent args)
    {
        var victim = args.Tripper;
        Spawn("EffectSparks", Transform(ent).Coordinates);
        // flare(): spark_system — искры с треском.
        _audio.PlayPvs(new SoundCollectionSpecifier("sparks"), ent);
        _popup.PopupEntity(Loc.GetString("imperial-trap-flare", ("trap", ent.Owner)), ent, PopupType.MediumCaution);
        ent.Comp.Charges--;
        if (ent.Comp.Charges <= 0)
            QueueDel(ent);

        if (_antimagic.CanBlockMagic(victim))
            return;

        switch (ent.Comp.Kind)
        {
            case ImperialTrapKind.Stun:
                _electrocution.TryDoElectrocution(victim, ent, 30, TimeSpan.FromSeconds(2), true, ignoreInsulation: true);
                _stun.TryUpdateParalyzeDuration(victim, TimeSpan.FromSeconds(10));
                break;
            case ImperialTrapKind.Fire:
                _popup.PopupEntity(Loc.GetString("imperial-trap-fire"), victim, victim, PopupType.LargeCaution);
                _stun.TryUpdateParalyzeDuration(victim, TimeSpan.FromSeconds(2));
                var trapXform = Transform(ent);
                if (trapXform.GridUid is { } trapGrid && TryComp<Robust.Shared.Map.Components.MapGridComponent>(trapGrid, out var trapMapGrid))
                    EntityManager.System<Content.Server.Atmos.EntitySystems.AtmosphereSystem>().HotspotExpose(trapGrid,
                        EntityManager.System<SharedMapSystem>().LocalToTile(trapGrid, trapMapGrid, trapXform.Coordinates), 700f, 50f, ent, true);
                if (TryComp<Content.Shared.Atmos.Components.FlammableComponent>(victim, out var flammable))
                {
                    EntityManager.System<Content.Server.Atmos.EntitySystems.FlammableSystem>().AdjustFireStacks(victim, 3, flammable, true);
                }
                break;
            case ImperialTrapKind.Chill:
                _popup.PopupEntity(Loc.GetString("imperial-trap-chill"), victim, victim, PopupType.LargeCaution);
                _stun.TryUpdateParalyzeDuration(victim, TimeSpan.FromSeconds(2));
                if (TryComp<Content.Shared.Temperature.Components.TemperatureComponent>(victim, out var temperature))
                    _temperature.ForceChangeTemperature(victim, Math.Max(2.7f, temperature.CurrentTemperature - 300), temperature);
                break;
            case ImperialTrapKind.Damage:
                _popup.PopupEntity(Loc.GetString("imperial-trap-earth"), victim, victim, PopupType.LargeCaution);
                _stun.TryUpdateParalyzeDuration(victim, TimeSpan.FromSeconds(10));
                _damage.TryChangeDamage(victim, new DamageSpecifier { DamageDict = { ["Blunt"] = 35 } }, origin: ent);
                var rock = Spawn("ImperialTrapRock", Transform(ent).Coordinates);
                EnsureComp<Robust.Shared.Spawners.TimedDespawnComponent>(rock).Lifetime = 20f;
                break;
        }
    }

    #endregion

    #region Проклятый вещмешок

    /// <summary>duffelbag: оглушение, 80 урона выносливости и голодный мешок на спину.</summary>
    private void OnDuffelbag(Entity<ImperialDuffelbagSpellComponent> ent, ref ImperialTouchHitEvent args)
    {
        if (!HasComp<HumanoidProfileComponent>(args.Target))
            return;

        args.Success = true;
        var victim = args.Target;
        if (_antimagic.CanBlockMagic(victim))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-duffel-blocked", ("target", victim)), args.Caster, args.Caster);
            return;
        }

        _flash.Flash(victim, args.Caster, null, TimeSpan.FromSeconds(2), 0.8f, false);
        _stun.TryUpdateParalyzeDuration(victim, TimeSpan.FromSeconds(5));
        _damage.TryChangeDamage(victim, new DamageSpecifier { DamageDict = { ["Stamina"] = 80 } }, origin: args.Caster);

        if (_inventory.TryGetSlotEntity(victim, "back", out var back) && HasComp<ImperialCurseOfHungerComponent>(back))
        {
            _popup.PopupEntity(Loc.GetString("imperial-spell-duffel-heavier"), victim, victim, PopupType.LargeCaution);
            return;
        }

        var bag = Spawn(ent.Comp.Bag, Transform(victim).Coordinates);
        _popup.PopupEntity(Loc.GetString("imperial-spell-duffel-victim"), victim, victim, PopupType.LargeCaution);
        if (back != null)
            _inventory.TryUnequip(victim, "back", true, true);
        if (!_inventory.TryEquip(victim, bag, "back", true, true))
            _hands.TryPickupAnyHand(victim, bag);
    }

    /// <summary>curse_of_hunger: голод растёт; голодный мешок кусает носителя.</summary>
    private void UpdateCursedBags()
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialCurseOfHungerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var curse, out var xform))
        {
            if (now < curse.NextTick)
                continue;

            curse.NextTick = now + TimeSpan.FromSeconds(10);
            curse.Hunger += 10;
            if (curse.Hunger < 100 || !HasComp<MobStateComponent>(xform.ParentUid))
                continue;

            var wearer = xform.ParentUid;
            _damage.TryChangeDamage(wearer, new DamageSpecifier { DamageDict = { ["Piercing"] = 10 } }, origin: uid);
            _popup.PopupEntity(Loc.GetString("imperial-spell-duffel-bite"), wearer, wearer, PopupType.MediumCaution);
        }
    }

    /// <summary>Кормление: еда утоляет голод; после пяти кормлений проклятие спадает.</summary>
    private void OnFeedBag(Entity<ImperialCurseOfHungerComponent> ent, ref Content.Shared.Interaction.InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<Content.Shared.Nutrition.Components.EdibleComponent>(args.Used))
            return;

        args.Handled = true;
        QueueDel(args.Used);
        ent.Comp.Hunger = 0;
        ent.Comp.Fed++;
        _popup.PopupEntity(Loc.GetString("imperial-spell-duffel-fed"), args.User, args.User);
        if (ent.Comp.Fed < 5)
            return;

        RemComp<Content.Shared.Interaction.Components.UnremoveableComponent>(ent);
        RemComp<ImperialCurseOfHungerComponent>(ent);
        _popup.PopupEntity(Loc.GetString("imperial-spell-duffel-sated"), args.User, args.User, PopupType.Medium);
    }

    #endregion

    private void UpdateDefensive()
    {
        UpdateTimestop();
        UpdatePhylacteries();
        UpdateSpacetime();
        UpdateCursedBags();
    }
}
