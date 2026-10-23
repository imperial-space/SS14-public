using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Objectives.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Actions;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Eye;
using Content.Shared.Fluids.Components;
using Content.Shared.Fluids;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Throwing;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard.Demons;

/// <summary>/mob/living/basic/demon/slaughter (и laughter): данные демона.</summary>
[RegisterComponent]
public sealed partial class ImperialSlaughterDemonComponent : Component
{
    /// <summary>Демон смеха: не убивает навсегда, а «приглашает на вечеринку».</summary>
    [DataField]
    public bool Laughter;

    /// <summary>objective_verb.</summary>
    [DataField]
    public LocId ObjectiveVerb = "imperial-demon-verb-kill";

    [DataField]
    public LocId Greeting = "imperial-demon-greeting-slaughter";

    [DataField]
    public SoundSpecifier DeathSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/demon_dies.ogg");

    /// <summary>death_drops.</summary>
    [DataField]
    public List<EntProtoId> Loot = new();

    /// <summary>summoner: разум мага, разбившего бутылку.</summary>
    [ViewVariables]
    public EntityUid? Summoner;

    [ViewVariables]
    public bool ObjectivesGiven;

    /// <summary>consume_count.</summary>
    [ViewVariables]
    public int ConsumeCount;
}

/// <summary>/datum/action/cooldown/spell/jaunt/bloodcrawl (+ slaughter_demon / funny).</summary>
[RegisterComponent]
public sealed partial class ImperialBloodCrawlComponent : Component
{
    /// <summary>Пожирать тех, кого тащишь в кровь (slaughter_demon).</summary>
    [DataField]
    public bool Voracious;

    /// <summary>Вечеринка: съеденные хранятся и возвращаются после смерти демона.</summary>
    [DataField]
    public bool Funny;

    [DataField]
    public TimeSpan ExitTime = TimeSpan.FromSeconds(2);

    [DataField]
    public float Radius = 1.5f;

    [DataField]
    public SoundSpecifier ConsumeSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/demon_consume.ogg");

    [DataField]
    public EntProtoId Action = "ImperialActionBloodCrawl";

    [ViewVariables]
    public EntityUid? ActionEntity;

    /// <summary>resist_jaunt_damage: первый раз и во время еды кровь не разъедает плоть.</summary>
    [ViewVariables]
    public bool ResistDamage = true;

    [ViewVariables]
    public TimeSpan NextDamage;

    [ViewVariables]
    public EntityUid? Consuming;

    [ViewVariables]
    public int ConsumeStep;

    /// <summary>consumed_mobs.</summary>
    [ViewVariables]
    public List<EntityUid> Consumed = new();
}

/// <summary>Демон сейчас в крови: невидим, бесплотен, не может действовать.</summary>
[RegisterComponent]
public sealed partial class ImperialBloodCrawlingComponent : Component
{
    [ViewVariables]
    public Dictionary<string, (int Layer, int Mask)> Fixtures = new();

    [ViewVariables]
    public bool HadIgnoreGravity;

    [ViewVariables]
    public bool Exiting;
}

/// <summary>movespeed_modifier/slaughter: рывок после выхода из крови.</summary>
[RegisterComponent]
public sealed partial class ImperialDemonSpeedBoostComponent : Component
{
    [ViewVariables]
    public TimeSpan Until;
}

/// <summary>/obj/item/organ/heart/demon: съевший получает кровавый прыжок.</summary>
[RegisterComponent]
public sealed partial class ImperialDemonHeartComponent : Component;

/// <summary>/obj/item/antag_spawner/slaughter_demon.</summary>
[RegisterComponent]
public sealed partial class ImperialDemonBottleComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Demon;

    [DataField]
    public LocId VeilMessage = "imperial-demon-bottle-veil";

    [ViewVariables]
    public bool Used;
}

/// <summary>«Убейте всех остальных заодно» — цель без провала.</summary>
[RegisterComponent]
public sealed partial class ImperialDemonFreeConditionComponent : Component;

/// <summary>
/// Демоны резни и смеха TG: кровавый прыжок, пожирание жертв, удар-бросок, сердце демона, бутылки мага.
/// </summary>
public sealed class ImperialSlaughterDemonSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly VisibilitySystem _visibility = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly TargetObjectiveSystem _targets = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedPuddleSystem _puddles = default!;
    [Dependency] private readonly Content.Server.NPC.Systems.NPCSystem _npc = default!;

    private const string FeastContainer = "imperial_demon_feast";
    private static readonly ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype> BloodReagent = "Blood";
    private static readonly TimeSpan JauntDamageInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ConsumeStepTime = TimeSpan.FromSeconds(3);
    private static readonly EntProtoId KillSummonerObjective = "ImperialDemonKillSummonerObjective";
    private static readonly EntProtoId KillEveryoneObjective = "ImperialDemonKillEveryoneObjective";

    /// <summary>Когда мы сами наносим урон в крови — его нельзя отменять.</summary>
    private bool _selfDamage;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialBloodCrawlComponent, MapInitEvent>(OnCrawlMapInit, after: new[] { typeof(Content.Server.NPC.HTN.HTNSystem) });
        SubscribeLocalEvent<ImperialBloodCrawlComponent, ImperialBloodCrawlActionEvent>(OnCrawlAction);
        SubscribeLocalEvent<ImperialBloodCrawlComponent, ImperialBloodCrawlExitDoAfterEvent>(OnExitDoAfter);
        SubscribeLocalEvent<ImperialBloodCrawlComponent, ImperialDemonConsumeDoAfterEvent>(OnConsumeDoAfter);
        SubscribeLocalEvent<ImperialBloodCrawlComponent, MobStateChangedEvent>(OnCrawlerStateChanged);

        SubscribeLocalEvent<ImperialBloodCrawlingComponent, InteractionAttemptEvent>(OnCrawlingInteract);
        SubscribeLocalEvent<ImperialBloodCrawlingComponent, AttackAttemptEvent>((_, _, args) => args.Cancel());
        SubscribeLocalEvent<ImperialBloodCrawlingComponent, PickupAttemptEvent>((_, _, args) => args.Cancel());
        SubscribeLocalEvent<ImperialBloodCrawlingComponent, BeforeDamageChangedEvent>(OnCrawlingDamage);

        SubscribeLocalEvent<ImperialSlaughterDemonComponent, MapInitEvent>(OnDemonMapInit, after: new[] { typeof(Content.Server.NPC.HTN.HTNSystem) });
        SubscribeLocalEvent<ImperialSlaughterDemonComponent, ImperialDemonSlamActionEvent>(OnSlam);
        SubscribeLocalEvent<ImperialSlaughterDemonComponent, MobStateChangedEvent>(OnDemonStateChanged);
        SubscribeLocalEvent<ImperialSlaughterDemonComponent, MindAddedMessage>(OnDemonMindAdded);

        SubscribeLocalEvent<ImperialDemonSpeedBoostComponent, RefreshMovementSpeedModifiersEvent>(OnSpeedBoost);
        SubscribeLocalEvent<ImperialDemonHeartComponent, UseInHandEvent>(OnHeartEaten);
        SubscribeLocalEvent<ImperialDemonBottleComponent, UseInHandEvent>(OnBottleUsed);
        SubscribeLocalEvent<ImperialDemonFreeConditionComponent, ObjectiveGetProgressEvent>((_, _, args) => args.Progress = 1f);

        SubscribeLocalEvent<MobStateChangedEvent>(OnAnyStateChanged);
    }

    private void OnCrawlMapInit(Entity<ImperialBloodCrawlComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    #region Кровавый прыжок

    /// <summary>find_nearby_blood(): лужа, в которой есть кровь.</summary>
    private EntityUid? FindNearbyBlood(EntityUid user, float radius)
    {
        foreach (var puddle in _lookup.GetEntitiesInRange<PuddleComponent>(Transform(user).Coordinates, radius))
        {
            if (!_solutions.ResolveSolution(puddle.Owner, puddle.Comp.SolutionName, ref puddle.Comp.Solution, out var solution))
                continue;

            if (solution.Contents.Any(r => r.Reagent.Prototype.Contains("Blood")))
                return puddle;
        }

        return null;
    }

    private void OnCrawlAction(Entity<ImperialBloodCrawlComponent> ent, ref ImperialBloodCrawlActionEvent args)
    {
        if (args.Handled)
            return;

        if (FindNearbyBlood(ent, ent.Comp.Radius) is not { } blood)
        {
            _popup.PopupEntity(Loc.GetString("imperial-bloodcrawl-no-blood"), ent, ent);
            return;
        }

        args.Handled = true;
        if (TryComp<ImperialBloodCrawlingComponent>(ent, out var crawling))
            TryExitCrawl(ent, crawling, blood);
        else
            EnterCrawl(ent, blood);
    }

    /// <summary>try_enter_jaunt(): погрузиться в кровь, прихватив того, кого тащишь.</summary>
    public void EnterCrawl(Entity<ImperialBloodCrawlComponent> ent, EntityUid? blood)
    {
        var comingWith = TryComp<PullerComponent>(ent, out var puller) ? puller.Pulling : null;
        var coords = blood is { } b ? Transform(b).Coordinates : Transform(ent).Coordinates;
        _transform.SetCoordinates(ent, coords);
        _physics.SetLinearVelocity(ent, System.Numerics.Vector2.Zero);

        var crawling = EnsureComp<ImperialBloodCrawlingComponent>(ent);
        if (comingWith is { } pulled && TryComp<PullableComponent>(pulled, out var pullable))
            _pulling.TryStopPull(pulled, pullable);

        _visibility.SetLayer(ent.Owner, (ushort) VisibilityFlags.Ghost);
        if (TryComp<EyeComponent>(ent, out var eye))
            _eye.SetVisibilityMask(ent, eye.VisibilityMask | (int) VisibilityFlags.Ghost, eye);

        if (TryComp<FixturesComponent>(ent, out var fixtures))
        {
            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                crawling.Fixtures[id] = (fixture.CollisionLayer, fixture.CollisionMask);
                _physics.SetCollisionLayer(ent, id, fixture, 0, fixtures);
                _physics.SetCollisionMask(ent, id, fixture, 0, fixtures);
            }
        }

        crawling.HadIgnoreGravity = HasComp<MovementIgnoreGravityComponent>(ent);
        EnsureComp<MovementIgnoreGravityComponent>(ent);
        _popup.PopupCoordinates(Loc.GetString("imperial-bloodcrawl-sink", ("user", ent.Owner)), coords, PopupType.MediumCaution);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/enter_blood.ogg"), coords);
        ent.Comp.NextDamage = _timing.CurTime + JauntDamageInterval;
        OnCrawl(ent);

        if (!ent.Comp.Voracious || comingWith is not { } victim || !HasComp<HumanoidProfileComponent>(victim))
            return;

        if (_mobs.IsAlive(victim))
        {
            _popup.PopupCoordinates(Loc.GetString("imperial-bloodcrawl-kicks-free", ("victim", victim)), coords, PopupType.MediumCaution);
            return;
        }

        var feast = _containers.EnsureContainer<Container>(ent, FeastContainer);
        _containers.Insert(victim, feast, force: true);
        _popup.PopupCoordinates(Loc.GetString("imperial-bloodcrawl-drags", ("user", ent.Owner), ("victim", victim)), coords, PopupType.LargeCaution);
        StartConsume(ent, victim);
    }

    /// <summary>consume_victim(): три стадии по 3 секунды.</summary>
    private void StartConsume(Entity<ImperialBloodCrawlComponent> ent, EntityUid victim)
    {
        ent.Comp.Consuming = victim;
        ent.Comp.ConsumeStep = 0;
        ent.Comp.ResistDamage = true;
        _popup.PopupEntity(Loc.GetString(ent.Comp.Funny ? "imperial-bloodcrawl-party-start" : "imperial-bloodcrawl-feast-start", ("victim", victim)), ent, ent, PopupType.Medium);
        NextConsumeStep(ent);
    }

    private void NextConsumeStep(Entity<ImperialBloodCrawlComponent> ent)
    {
        _audio.PlayPvs(ent.Comp.ConsumeSound, ent);
        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, ConsumeStepTime, new ImperialDemonConsumeDoAfterEvent(), ent)
        {
            BreakOnMove = true,
            MovementThreshold = 1f,
            RequireCanInteract = false,
        });

        if (!started)
            FailConsume(ent);
    }

    private void FailConsume(Entity<ImperialBloodCrawlComponent> ent)
    {
        _popup.PopupEntity(Loc.GetString("imperial-bloodcrawl-lost-victim"), ent, ent);
        if (ent.Comp.Consuming is { } victim && !TerminatingOrDeleted(victim))
        {
            _containers.TryRemoveFromContainer(victim, true);
            _transform.SetCoordinates(victim, Transform(ent).Coordinates);
        }

        ent.Comp.Consuming = null;
        ent.Comp.ResistDamage = false;
    }

    private void OnConsumeDoAfter(Entity<ImperialBloodCrawlComponent> ent, ref ImperialDemonConsumeDoAfterEvent args)
    {
        if (args.Handled || ent.Comp.Consuming is not { } victim)
            return;

        args.Handled = true;
        if (args.Cancelled || TerminatingOrDeleted(victim))
        {
            FailConsume(ent);
            return;
        }

        ent.Comp.ConsumeStep++;
        if (ent.Comp.ConsumeStep < 3)
        {
            NextConsumeStep(ent);
            return;
        }

        ent.Comp.Consuming = null;
        ent.Comp.ResistDamage = false;
        ent.Comp.NextDamage = _timing.CurTime + JauntDamageInterval;
        RaiseLocalEvent(ent, new RejuvenateEvent());

        // victim.death(): в SS14 урон 1000 разорвал бы тело, а гости вечеринки должны вернуться.
        _mobs.ChangeMobState(victim, MobState.Dead);
        if (TryComp<ImperialSlaughterDemonComponent>(ent, out var demon))
            demon.ConsumeCount++;

        if (ent.Comp.Funny)
        {
            ent.Comp.Consumed.Add(victim);
            _popup.PopupEntity(Loc.GetString("imperial-bloodcrawl-party-joined", ("victim", victim)), ent, ent, PopupType.Medium);
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-bloodcrawl-devoured", ("victim", victim)), ent, ent, PopupType.Medium);
        QueueDel(victim);
    }

    /// <summary>try_exit_jaunt(): лужа начинает пузыриться, через 2 секунды демон выходит.</summary>
    private void TryExitCrawl(Entity<ImperialBloodCrawlComponent> ent, ImperialBloodCrawlingComponent crawling, EntityUid blood)
    {
        if (ent.Comp.Consuming != null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-bloodcrawl-cant-exit"), ent, ent);
            return;
        }

        if (crawling.Exiting)
            return;

        crawling.Exiting = true;
        _popup.PopupEntity(Loc.GetString("imperial-bloodcrawl-bubbles"), blood, PopupType.MediumCaution);
        var started = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, ent, ent.Comp.ExitTime, new ImperialBloodCrawlExitDoAfterEvent(), ent, blood)
        {
            BreakOnMove = true,
            RequireCanInteract = false,
            DistanceThreshold = ent.Comp.Radius + 0.5f,
        });

        if (!started)
            crawling.Exiting = false;
    }

    private void OnExitDoAfter(Entity<ImperialBloodCrawlComponent> ent, ref ImperialBloodCrawlExitDoAfterEvent args)
    {
        if (!TryComp<ImperialBloodCrawlingComponent>(ent, out var crawling))
            return;

        crawling.Exiting = false;
        if (args.Cancelled || args.Handled || args.Target is not { } blood || TerminatingOrDeleted(blood))
            return;

        args.Handled = true;
        _transform.SetCoordinates(ent, Transform(blood).Coordinates);
        ExitCrawl(ent, crawling);
        _popup.PopupEntity(Loc.GetString("imperial-bloodcrawl-rises", ("user", ent.Owner)), ent, PopupType.LargeCaution);
    }

    /// <summary>exit_jaunt() + exit_blood_effect().</summary>
    public void ExitCrawl(Entity<ImperialBloodCrawlComponent> ent, ImperialBloodCrawlingComponent crawling)
    {
        _visibility.SetLayer(ent.Owner, (ushort) VisibilityFlags.Normal);
        if (TryComp<EyeComponent>(ent, out var eye))
            _eye.SetVisibilityMask(ent, eye.VisibilityMask & ~(int) VisibilityFlags.Ghost, eye);

        if (TryComp<FixturesComponent>(ent, out var fixtures))
        {
            foreach (var (id, fixture) in fixtures.Fixtures)
            {
                if (!crawling.Fixtures.TryGetValue(id, out var saved))
                    continue;

                _physics.SetCollisionLayer(ent, id, fixture, saved.Layer, fixtures);
                _physics.SetCollisionMask(ent, id, fixture, saved.Mask, fixtures);
            }
        }

        if (!crawling.HadIgnoreGravity)
            RemComp<MovementIgnoreGravityComponent>(ent);

        RemComp<ImperialBloodCrawlingComponent>(ent);
        ent.Comp.ResistDamage = false;
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/exit_blood.ogg"), ent);
        OnCrawl(ent);

        // on_crawl(): рывок, только если мы уже в мире смертных.
        var boost = EnsureComp<ImperialDemonSpeedBoostComponent>(ent);
        boost.Until = _timing.CurTime + TimeSpan.FromSeconds(6);
        _speed.RefreshMovementSpeedModifiers(ent);
    }

    private void OnCrawl(EntityUid uid)
    {
        // Серии ударов TG усиливают раны — в SS14 ран нет, сбрасывать нечего.
    }

    private void OnCrawlingInteract(Entity<ImperialBloodCrawlingComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnCrawlingDamage(Entity<ImperialBloodCrawlingComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (!_selfDamage)
            args.Cancelled = true;
    }

    private void OnCrawlerStateChanged(Entity<ImperialBloodCrawlComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Alive || !TryComp<ImperialBloodCrawlingComponent>(ent, out var crawling))
            return;

        ExitCrawl(ent, crawling);
    }

    private void OnSpeedBoost(Entity<ImperialDemonSpeedBoostComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (_timing.CurTime < ent.Comp.Until)
            args.ModifySpeed(1.5f);
    }

    #endregion

    #region Демон

    /// <summary>Без игрока демон ничего не делает (в TG его просто нет, пока не нашёлся призрак).</summary>
    private void OnDemonMapInit(Entity<ImperialSlaughterDemonComponent> ent, ref MapInitEvent args)
    {
        _npc.SleepNPC(ent);
    }

    /// <summary>bodyslam(): 20 урона и бросок на экран вперёд.</summary>
    private void OnSlam(Entity<ImperialSlaughterDemonComponent> ent, ref ImperialDemonSlamActionEvent args)
    {
        if (args.Handled || args.Target == ent.Owner || !HasComp<Content.Shared.Mobs.Components.MobStateComponent>(args.Target)
            || HasComp<ImperialBloodCrawlingComponent>(ent))
            return;

        var target = args.Target;
        var delta = _transform.GetWorldPosition(target) - _transform.GetWorldPosition(ent);
        if (delta.Length() > 1.6f)
        {
            _popup.PopupEntity(Loc.GetString("imperial-demon-slam-far", ("target", target)), ent, ent);
            return;
        }

        args.Handled = true;
        _damage.TryChangeDamage(target, new DamageSpecifier { DamageDict = { ["Blunt"] = 20 } }, origin: ent);
        _popup.PopupEntity(Loc.GetString("imperial-demon-slam", ("user", ent.Owner), ("target", target)), target, PopupType.LargeCaution);
        var dir = delta.LengthSquared() > 0.01f ? delta.Normalized() : _transform.GetWorldRotation(ent).ToWorldVec();
        _throwing.TryThrow(target, dir * 10f, 5f, ent);
    }

    /// <summary>mind_initialize(): цели и приветствие при вселении игрока.</summary>
    private void OnDemonMindAdded(Entity<ImperialSlaughterDemonComponent> ent, ref MindAddedMessage args)
    {
        if (ent.Comp.ObjectivesGiven)
            return;

        ent.Comp.ObjectivesGiven = true;
        var mindId = args.Mind.Owner;
        var mind = args.Mind.Comp;
        var verb = Loc.GetString(ent.Comp.ObjectiveVerb);
        if (ent.Comp.Summoner is { } summoner && TryComp<MindComponent>(summoner, out var summonerMind)
            && _objectives.TryCreateObjective(mindId, mind, KillSummonerObjective) is { } kill)
        {
            _targets.SetTarget(kill, summoner);
            _meta.SetEntityName(kill, Loc.GetString("imperial-demon-objective-summoner", ("verb", verb), ("name", summonerMind.CharacterName ?? "?")));
            _mind.AddObjective(mindId, mind, kill);
        }

        if (_objectives.TryCreateObjective(mindId, mind, KillEveryoneObjective) is { } everyone)
        {
            _meta.SetEntityName(everyone, Loc.GetString(ent.Comp.Summoner != null ? "imperial-demon-objective-everyone-else" : "imperial-demon-objective-everyone", ("verb", verb)));
            _mind.AddObjective(mindId, mind, everyone);
        }

        _audio.PlayEntity(new SoundPathSpecifier("/Audio/Imperial/Wizard/demon_dies.ogg"), ent, ent);
        if (mind.UserId is { } userId && _players.TryGetSessionById(userId, out var session))
            _chatManager.DispatchServerMessage(session, Loc.GetString(ent.Comp.Greeting), true);
    }

    /// <summary>DEL_ON_DEATH + death_drops; у демона смеха — котёнок и освобождение гостей.</summary>
    private void OnDemonStateChanged(Entity<ImperialSlaughterDemonComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var coords = _transform.GetMapCoordinates(ent);
        _popup.PopupCoordinates(Loc.GetString(ent.Comp.Laughter ? "imperial-demon-death-laughter" : "imperial-demon-death", ("demon", ent.Owner)),
            Transform(ent).Coordinates, PopupType.LargeCaution);
        _audio.PlayPvs(ent.Comp.DeathSound, Transform(ent).Coordinates);

        if (TryComp<ImperialBloodCrawlComponent>(ent, out var crawl))
            ReleaseFriends((ent, crawl));

        if (!ent.Comp.Laughter)
        {
            var blood = new Solution();
            blood.AddReagent(BloodReagent, 50);
            _puddles.TrySpillAt(Transform(ent).Coordinates, blood, out _, false);
        }

        foreach (var loot in ent.Comp.Loot)
        {
            var spawned = Spawn(loot, coords);
            if (ent.Comp.Laughter)
                _meta.SetEntityName(spawned, Loc.GetString("imperial-demon-kitten-name"));
        }

        QueueDel(ent);
    }

    /// <summary>funny/on_death(): все гости вечеринки возвращаются к жизни.</summary>
    private void ReleaseFriends(Entity<ImperialBloodCrawlComponent> ent)
    {
        if (_containers.TryGetContainer(ent, FeastContainer, out var feast))
        {
            foreach (var friend in feast.ContainedEntities.ToList())
            {
                _containers.Remove(friend, feast, force: true);
                _transform.SetCoordinates(friend, Transform(ent).Coordinates);
                if (!ent.Comp.Funny)
                    continue;

                RaiseLocalEvent(friend, new RejuvenateEvent());
                _audio.PlayEntity(new SoundPathSpecifier("/Audio/Imperial/Wizard/exit_blood.ogg"), friend, friend);
                _popup.PopupEntity(Loc.GetString("imperial-bloodcrawl-party-over", ("demon", ent.Owner)), friend, friend, PopupType.Large);
            }
        }

        ent.Comp.Consumed.Clear();
    }

    /// <summary>on_victim_statchange(): ожившего гостя выбрасывает наружу.</summary>
    private void OnAnyStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead || !_containers.TryGetContainingContainer((args.Target, null), out var container)
            || container.ID != FeastContainer || !TryComp<ImperialBloodCrawlComponent>(container.Owner, out var crawl))
            return;

        _containers.Remove(args.Target, container, force: true);
        _transform.SetCoordinates(args.Target, Transform(container.Owner).Coordinates);
        crawl.Consumed.Remove(args.Target);
        _popup.PopupEntity(Loc.GetString("imperial-bloodcrawl-fell-out", ("victim", args.Target)), args.Target, PopupType.MediumCaution);
    }

    #endregion

    #region Предметы

    /// <summary>heart/demon/attack(): съесть сердце и обрести кровавый прыжок.</summary>
    private void OnHeartEaten(Entity<ImperialDemonHeartComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        _popup.PopupEntity(Loc.GetString("imperial-demon-heart-eat", ("user", user)), user, PopupType.MediumCaution);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/demon_consume.ogg"), user);
        QueueDel(ent);
        if (HasComp<ImperialBloodCrawlComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("imperial-demon-heart-nothing"), user, user);
            return;
        }

        var crawl = EnsureComp<ImperialBloodCrawlComponent>(user);
        _actions.AddAction(user, ref crawl.ActionEntity, crawl.Action);
        _popup.PopupEntity(Loc.GetString("imperial-demon-heart-power"), user, user, PopupType.Large);
    }

    /// <summary>antag_spawner/slaughter_demon/attack_self(): разбить бутылку на станции.</summary>
    private void OnBottleUsed(Entity<ImperialDemonBottleComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Used)
            return;

        args.Handled = true;
        var user = args.User;
        if (_station.GetOwningStation(user) == null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-demon-bottle-station"), user, user);
            return;
        }

        ent.Comp.Used = true;
        if (TryComp<ImperialSpellbookRefundableItemComponent>(ent, out var refundable))
            refundable.Used = true;

        var demon = Spawn(ent.Comp.Demon, _transform.GetMapCoordinates(user));
        if (TryComp<ImperialSlaughterDemonComponent>(demon, out var demonComp) && _mind.TryGetMind(user, out var mindId, out _))
            demonComp.Summoner = mindId;

        if (TryComp<ImperialBloodCrawlComponent>(demon, out var crawl))
        {
            var blood = new Solution();
            blood.AddReagent(BloodReagent, 20);
            _puddles.TrySpillAt(Transform(user).Coordinates, blood, out _, false);
            EnterCrawl((demon, crawl), null);
        }

        _popup.PopupEntity(Loc.GetString("imperial-demon-bottle-shatter"), user, user);
        _popup.PopupEntity(Loc.GetString(ent.Comp.VeilMessage), user, user, PopupType.MediumCaution);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/glass_break1.ogg"), user);
        QueueDel(ent);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ImperialBloodCrawlingComponent, ImperialBloodCrawlComponent>();
        while (query.MoveNext(out var uid, out _, out var crawl))
        {
            // damage_for_lazy_demon(): 5% здоровья каждые 20 секунд в крови.
            if (!crawl.Voracious || crawl.ResistDamage || now < crawl.NextDamage)
                continue;

            crawl.NextDamage = now + JauntDamageInterval;
            _selfDamage = true;
            _damage.TryChangeDamage(uid, new DamageSpecifier { DamageDict = { ["Blunt"] = 10 } }, true);
            _selfDamage = false;
            _popup.PopupEntity(Loc.GetString("imperial-bloodcrawl-dissolving"), uid, uid, PopupType.MediumCaution);
        }

        var boosts = EntityQueryEnumerator<ImperialDemonSpeedBoostComponent>();
        while (boosts.MoveNext(out var uid, out var boost))
        {
            if (now < boost.Until)
                continue;

            RemComp<ImperialDemonSpeedBoostComponent>(uid);
            _speed.RefreshMovementSpeedModifiers(uid);
        }
    }
}
