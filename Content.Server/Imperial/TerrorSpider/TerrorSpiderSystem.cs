using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Pinpointer;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Gibbing;
using Content.Shared.Imperial.TerrorSpider;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>
/// Пауки Ужаса 1 в 1 с Paradise (terror_spiders.dm, terror_ai.dm, hive.dm, reproduction.dm и типы пауков).
/// </summary>
public sealed partial class TerrorSpiderSystem : EntitySystem
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    /// <summary>Тик Life() simple_animal — SSmobs раз в 2 секунды.</summary>
    public static readonly TimeSpan LifeInterval = TimeSpan.FromSeconds(2);

    /// <summary>DECOMPOSE_TIMER.</summary>
    private static readonly TimeSpan DecomposeTime = TimeSpan.FromMinutes(5);

    /// <summary>ts_death_window: 15 минут после смерти паука ИИ ползает по трубам чаще.</summary>
    private static readonly TimeSpan DeathWindow = TimeSpan.FromMinutes(15);

    private static readonly EntProtoId WebAction = "ActionTerrorSpiderWeb";
    private static readonly EntProtoId WrapAction = "ActionTerrorSpiderWrap";
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    /// <summary>ts_count_dead / ts_death_last.</summary>
    private int _countDead;
    private TimeSpan _deathLast;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TerrorSpiderComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TerrorSpiderComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TerrorSpiderComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<TerrorSpiderComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<TerrorSpiderComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<TerrorSpiderComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<TerrorSpiderComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<TerrorSpiderComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ =>
        {
            _countDead = 0;
            _deathLast = TimeSpan.Zero;
        });

        InitializeAbilities();
        InitializeHive();
        InitializeEffects();
    }

    private void OnMapInit(Entity<TerrorSpiderComponent> ent, ref MapInitEvent args)
    {
        var now = _timing.CurTime;
        ent.Comp.CreatedAt = now;
        ent.Comp.NextLife = now + LifeInterval;

        if (ent.Comp.Web != null)
            _actions.AddAction(ent, WebAction);

        // Обматывать могут только те, кому это нужно для регенерации.
        if (ent.Comp.RegenPointsPerTick < ent.Comp.RegenPointsPerHp)
            _actions.AddAction(ent, WrapAction);

        foreach (var action in ent.Comp.Actions)
        {
            _actions.AddAction(ent, action);
        }

        // name += " ([rand(1, 1000)])"
        _metaData.SetEntityName(ent, $"{Name(ent)} ({_random.Next(1, 1001)})");

        MsgTerrorSpiders(Loc.GetString("terror-spider-sense-grown", ("spider", ent.Owner), ("area", AreaName(ent))));

        if (TryComp<TerrorSpiderQueenComponent>(ent, out var queen))
            OnQueenInit((ent, ent.Comp, queen));
    }

    private void OnShutdown(Entity<TerrorSpiderComponent> ent, ref ComponentShutdown args)
    {
        HandleDying(ent);
    }

    /// <summary>handle_dying().</summary>
    private void HandleDying(Entity<TerrorSpiderComponent> ent)
    {
        if (ent.Comp.HasDied)
            return;

        ent.Comp.HasDied = true;
        _countDead++;
        _deathLast = _timing.CurTime;
    }

    private void OnMobStateChanged(Entity<TerrorSpiderComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (!ent.Comp.HasDied)
        {
            MsgTerrorSpiders(Loc.GetString("terror-spider-sense-died", ("spider", ent.Owner), ("area", AreaName(ent))));
            ent.Comp.DecomposeAt = _timing.CurTime + DecomposeTime;
        }

        HandleDying(ent);
        StopRemoteView(ent);
        OnSpiderDeath(ent);
    }

    /// <summary>give_intro_text(): при вселении игрока.</summary>
    private void OnMindAdded(Entity<TerrorSpiderComponent> ent, ref MindAddedMessage args)
    {
        if (ent.Comp.IntroShown || !TryComp<ActorComponent>(ent, out var actor))
            return;

        ent.Comp.IntroShown = true;
        var session = actor.PlayerSession;
        _audio.PlayGlobal(new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Imperial/TerrorSpider/terrorspider.ogg"), session);
        SendTo(session, Loc.GetString("terror-spider-intro-header"));
        SendTo(session, Loc.GetString(ent.Comp.IntroText));
    }

    private void OnExamined(Entity<TerrorSpiderComponent> ent, ref ExaminedEvent args)
    {
        if (_mobState.IsDead(ent))
            return;

        if (HasComp<ActorComponent>(ent))
            args.PushMarkup(Loc.GetString("terror-spider-examine-intelligent"));

        if (_thresholds.TryGetThresholdForState(ent, MobState.Dead, out var max) && TryComp<DamageableComponent>(ent, out var damageable))
        {
            var health = (float) (max.Value - _damageable.GetTotalDamage((ent.Owner, damageable)));
            var maxHealth = (float) max.Value;
            var key = health > maxHealth * 0.95f ? "terror-spider-examine-health-excellent"
                : health > maxHealth * 0.75f ? "terror-spider-examine-health-few"
                : health > maxHealth * 0.55f ? "terror-spider-examine-health-many"
                : health > maxHealth * 0.25f ? "terror-spider-examine-health-barely"
                : null;
            if (key != null)
                args.PushMarkup(Loc.GetString(key));

            if (ent.Comp.Degenerate)
                args.PushMarkup(Loc.GetString("terror-spider-examine-dying"));
            else if (health < maxHealth && ent.Comp.RegenPoints > ent.Comp.RegenPointsPerKill)
                args.PushMarkup(Loc.GetString("terror-spider-examine-regenerating"));
        }

        if (ent.Comp.KillCount >= 1)
            args.PushMarkup(Loc.GetString("terror-spider-examine-blood"));

        if (TryComp<TerrorSpiderQueenComponent>(ent, out var queen) && HasComp<ActorComponent>(ent)
            && (HasComp<TerrorSpiderComponent>(args.Examiner) || HasComp<Content.Shared.Ghost.GhostComponent>(args.Examiner)))
        {
            args.PushMarkup(Loc.GetString("terror-spider-examine-eggs-laid", ("count", queen.EggsLaid)));
            args.PushMarkup(Loc.GetString("terror-spider-examine-minutes", ("minutes", MinutesAlive(ent.Comp))));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<TerrorSpiderComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var spider, out var mobState))
        {
            if (spider.DecomposeAt is { } decompose && now >= decompose)
            {
                // decompose_now()
                _popup.PopupEntity(Loc.GetString("terror-spider-decompose", ("spider", uid)), uid);
                _gibbing.Gib(uid);
                continue;
            }

            if (now < spider.NextLife)
                continue;

            spider.NextLife = now + LifeInterval;
            if (mobState.CurrentState == MobState.Dead)
                continue;

            Life((uid, spider));
        }

        UpdateHive(now);
        UpdateEffects(now);
        UpdateAi(now);
    }

    /// <summary>terror_spider/Life().</summary>
    private void Life(Entity<TerrorSpiderComponent> ent)
    {
        var spider = ent.Comp;
        if (spider.Degenerate)
            Damage(ent, "Poison", _random.Next(1, 11));

        if (spider.RegenPoints < spider.RegenPointsMax)
            spider.RegenPoints += spider.RegenPointsPerTick;

        if (TryComp<DamageableComponent>(ent, out var damageable) && spider.RegenPoints > spider.RegenPointsPerHp)
        {
            var groups = _damageable.GetDamagePerGroup((ent.Owner, damageable));
            var brute = groups.GetValueOrDefault(BruteGroup);
            var burn = groups.GetValueOrDefault(BurnGroup);
            if (brute > 0)
            {
                HealGroup(ent, "Brute");
                spider.RegenPoints -= spider.RegenPointsPerHp;
            }
            else if (burn > 0)
            {
                HealGroup(ent, "Burn");
                spider.RegenPoints -= spider.RegenPointsPerHp;
            }
        }

        if (TryComp<TerrorSpiderPurpleComponent>(ent, out var purple))
            PurpleLife((ent, spider, purple));

        if (TryComp<TerrorSpiderQueenComponent>(ent, out var queen))
            QueenLife((ent, spider, queen));
    }

    private void HealGroup(EntityUid uid, string group)
    {
        if (!_proto.TryIndex<DamageGroupPrototype>(group, out var proto))
            return;
        _damageable.TryChangeDamage(uid, new DamageSpecifier(proto, FixedPoint2.New(-1)), true, false);
    }

    private void Damage(EntityUid uid, string type, float amount, EntityUid? origin = null)
    {
        _damageable.TryChangeDamage(uid, new DamageSpecifier { DamageDict = { [type] = amount } }, true, origin: origin);
    }

    /// <summary>adjustHealth: удалённый обзор сбрасывается при атаке; adjustBruteLoss/FireLoss → Retaliate().</summary>
    private void OnDamageChanged(Entity<TerrorSpiderComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta is not { } delta || !args.DamageIncreased)
            return;

        if (ent.Comp.Watching != null)
        {
            StopRemoteView(ent);
            _popup.PopupEntity(Loc.GetString("terror-spider-remote-view-cancelled"), ent, ent, PopupType.SmallCaution);
        }

        if (args.Origin is { } origin && !HasComp<TerrorSpiderComponent>(origin))
            Retaliate(ent, origin);
    }

    /// <summary>
    /// force_threshold королевы (18): оружие и снаряды слабее не наносят урона.
    /// </summary>
    private void OnBeforeDamage(Entity<TerrorSpiderComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Origin == null || !TryComp<TerrorSpiderQueenComponent>(ent, out var queen) || queen.Empress)
            return;

        var total = args.Damage.GetTotal();
        if (total > 0 && total < 18)
            args.Cancelled = true;
    }

    /// <summary>Retaliate(): враги атакующего становятся врагами всех ближайших пауков.</summary>
    private void Retaliate(Entity<TerrorSpiderComponent> ent, EntityUid attacker)
    {
        ent.Comp.Enemies.Add(attacker);
        foreach (var other in _lookup.GetEntitiesInRange<TerrorSpiderComponent>(_transform.GetMapCoordinates(ent), 7))
        {
            other.Comp.Enemies.Add(attacker);
        }
    }

    #region TerrorSense

    /// <summary>msg_terrorspiders(): «TerrorSense: ...» всем живым паукам.</summary>
    public void MsgTerrorSpiders(string text)
    {
        var message = Loc.GetString("terror-spider-sense", ("message", text));
        var query = EntityQueryEnumerator<TerrorSpiderComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out _, out var actor))
        {
            if (_mobState.IsDead(uid))
                continue;
            SendTo(actor.PlayerSession, message);
        }
    }

    private void SendTo(ICommonSession session, string text)
    {
        _chat.ChatMessageToOne(ChatChannel.Server, text, text, EntityUid.Invalid, false, session.Channel);
    }

    private void SendTo(EntityUid uid, string text)
    {
        if (TryComp<ActorComponent>(uid, out var actor))
            SendTo(actor.PlayerSession, text);
    }

    /// <summary>get_area(): ближайший навигационный маяк.</summary>
    public string AreaName(EntityUid uid)
    {
        var pos = _transform.GetMapCoordinates(uid);
        string? best = null;
        var bestDist = float.MaxValue;
        var query = EntityQueryEnumerator<NavMapBeaconComponent, TransformComponent>();
        while (query.MoveNext(out _, out var beacon, out var xform))
        {
            if (string.IsNullOrWhiteSpace(beacon.Text) || xform.MapID != pos.MapId)
                continue;

            var dist = (_transform.GetWorldPosition(xform) - pos.Position).LengthSquared();
            if (dist < bestDist)
            {
                bestDist = dist;
                best = beacon.Text;
            }
        }

        return best ?? Loc.GetString("terror-spider-area-unknown");
    }

    #endregion

    #region Атака

    /// <summary>AttackingTarget() и spider_specialattack().</summary>
    private void OnMeleeHit(Entity<TerrorSpiderComponent> ent, ref MeleeHitEvent args)
    {
        if (args.User != ent.Owner || !args.IsHit || args.HitEntities.Count == 0)
            return;

        var target = args.HitEntities[0];

        // Другие пауки: съёживается / трётся носом. Только тир 4+ бьёт младших.
        if (TryComp<TerrorSpiderComponent>(target, out var other))
        {
            other.Enemies.Remove(ent);
            ent.Comp.Enemies.Remove(target);
            var key = other.Tier > ent.Comp.Tier ? "terror-spider-cowers"
                : other.Tier == ent.Comp.Tier ? "terror-spider-nuzzles"
                : ent.Comp.Tier >= 4 ? null
                : "terror-spider-nuzzles-harmless";
            if (key != null)
            {
                _popup.PopupEntity(Loc.GetString(key, ("spider", ent.Owner), ("target", target)), ent);
                args.Handled = true;
                return;
            }
        }
        else if (HasComp<TerrorRoyalJellyComponent>(target))
        {
            ConsumeJelly(ent, target);
            args.Handled = true;
            return;
        }
        else if (HasComp<TerrorWebComponent>(target) || HasComp<TerrorEggClusterComponent>(target) || HasComp<TerrorCocoonComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("terror-spider-dont-destroy"), ent, ent);
            args.Handled = true;
            return;
        }

        // Случайный урон в пределах melee_damage_lower..upper поверх базового.
        var (lower, upper) = MeleeRange(ent);
        var roll = lower + _random.NextFloat() * (upper - lower);
        var baseDamage = (float) args.BaseDamage.DamageDict.GetValueOrDefault("Piercing");
        var bonus = roll - baseDamage;

        if (ent.Comp.SmashWalls && _tag.HasTag(target, WallTag))
            args.BonusDamage += new DamageSpecifier { DamageDict = { ["Structural"] = 1000 } };

        if (!HasComp<MobStateComponent>(target) || _mobState.IsDead(target))
        {
            if (bonus != 0)
                args.BonusDamage += new DamageSpecifier { DamageDict = { ["Piercing"] = bonus } };
            return;
        }

        // Серый: двойной урон по жертве в паутине.
        if (ent.Comp.Attack == TerrorSpiderAttackType.Gray)
        {
            if (OnWeb(target))
            {
                roll = lower * 2 + _random.NextFloat() * (upper - lower) * 2;
                bonus = roll - baseDamage;
                _popup.PopupEntity(Loc.GetString("terror-spider-gray-maul", ("spider", ent.Owner), ("target", target)), ent, PopupType.MediumCaution);
            }
        }

        if (bonus != 0)
            args.BonusDamage += new DamageSpecifier { DamageDict = { ["Piercing"] = bonus } };

        SpecialAttack(ent, target);
    }

    /// <summary>Урон красного зависит от ярости.</summary>
    private (float Lower, float Upper) MeleeRange(Entity<TerrorSpiderComponent> ent)
    {
        if (!TryComp<TerrorSpiderRedComponent>(ent, out var red))
            return (ent.Comp.MeleeLower, ent.Comp.MeleeUpper);

        UpdateEnrage((ent, ent.Comp, red));
        return red.Enrage switch
        {
            0 => (15, 20),
            1 => (15, 35),
            _ => (35, 40),
        };
    }

    /// <summary>red/AttackingTarget(): ступени ярости по здоровью.</summary>
    private void UpdateEnrage(Entity<TerrorSpiderComponent, TerrorSpiderRedComponent> ent)
    {
        if (!TryComp<DamageableComponent>(ent, out var damageable)
            || !_thresholds.TryGetThresholdForState(ent, MobState.Dead, out var max))
            return;

        var maxHealth = (float) max.Value;
        var health = maxHealth - (float) _damageable.GetTotalDamage((ent.Owner, damageable));
        var red = ent.Comp2;
        switch (red.Enrage)
        {
            case 0 when health < maxHealth:
                red.Enrage = 1;
                _popup.PopupEntity(Loc.GetString("terror-spider-red-enrage-1", ("spider", ent.Owner)), ent, PopupType.MediumCaution);
                break;
            case 1 when health >= maxHealth:
                red.Enrage = 0;
                _popup.PopupEntity(Loc.GetString("terror-spider-red-calm", ("spider", ent.Owner)), ent);
                break;
            case 1 when health < maxHealth / 2:
                red.Enrage = 2;
                _popup.PopupEntity(Loc.GetString("terror-spider-red-enrage-2", ("spider", ent.Owner)), ent, PopupType.MediumCaution);
                break;
            case 2 when health > maxHealth / 2:
                red.Enrage = 1;
                _popup.PopupEntity(Loc.GetString("terror-spider-red-calm", ("spider", ent.Owner)), ent);
                break;
        }
    }

    #endregion

    /// <summary>MinutesAlive().</summary>
    public int MinutesAlive(TerrorSpiderComponent spider)
    {
        return (int) ((_timing.CurTime - spider.CreatedAt).TotalMinutes);
    }

    /// <summary>Недавно погиб паук — ИИ ползает по трубам чаще.</summary>
    private bool RecentDeath(TimeSpan now)
    {
        return _countDead > 0 && now < _deathLast + DeathWindow;
    }

    public IEnumerable<Entity<TerrorSpiderComponent>> LivingSpiders()
    {
        var query = EntityQueryEnumerator<TerrorSpiderComponent>();
        var list = new List<Entity<TerrorSpiderComponent>>();
        while (query.MoveNext(out var uid, out var spider))
        {
            if (!_mobState.IsDead(uid))
                list.Add((uid, spider));
        }

        return list;
    }

    [Dependency] private readonly MetaDataSystem _metaData = default!;
}
