using System.Linq;
using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Atmos.Piping.Components;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.Chat.Managers;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Alert;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Morph;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Stunnable;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Morph;

/// <summary>
/// Морф 1 в 1 с Paradise (code/game/gamemodes/miniantags/morph): маскировка под любой предмет,
/// засада, поедание трупов и предметов, проход через шлюзы, кислота на вентиляции, размножение.
/// </summary>
public sealed partial class MorphSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedSuitSensorSystem _sensors = default!;
    [Dependency] private readonly SharedToolSystem _tools = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly WeldableSystem _weldable = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public const string StomachContainer = "morph_stomach";

    private static readonly SoundCollectionSpecifier BoneBreak = new("ImperialMorphBoneBreak");
    private static readonly SoundPathSpecifier MutateSound = new("/Audio/Imperial/Morph/mutate.ogg");
    private static readonly SoundPathSpecifier SliceSound = new("/Audio/Imperial/Morph/slice.ogg");
    private static readonly SoundPathSpecifier SplatSound = new("/Audio/Imperial/Morph/splat.ogg");
    private static readonly SoundPathSpecifier WelderSound = new("/Audio/Items/welder.ogg");

    private static readonly ProtoId<AlertPrototype> AmbushAlert = "MorphAmbush";
    private static readonly ProtoId<AlertPrototype> PerfectAmbushAlert = "MorphAmbushPerfect";
    private static readonly EntProtoId MorphMindRole = "MindRoleMorph";
    private static readonly EntProtoId EatObjective = "MorphEatObjective";
    private static readonly EntProtoId ProcreateObjective = "MorphProcreateObjective";

    /// <summary>Дистанция, с которой при осмотре видно «что-то не так».</summary>
    private const float WrongnessRange = 3f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MorphComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MorphComponent, MindAddedMessage>(OnMindAdded);
        SubscribeLocalEvent<MorphComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<MorphComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<MorphComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<MorphComponent, BeingGibbedEvent>(OnGibbed);
        SubscribeLocalEvent<MorphComponent, MoveEvent>(OnMove);

        SubscribeLocalEvent<MorphComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<MorphComponent, UserActivateInWorldEvent>(OnUserActivate);
        SubscribeLocalEvent<MorphComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<MorphComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<MorphComponent, InteractUsingEvent>(OnInteractUsing);

        SubscribeLocalEvent<MorphComponent, MorphEatDoAfterEvent>(OnEatDoAfter);
        SubscribeLocalEvent<MorphComponent, MorphPryJawsDoAfterEvent>(OnPryJawsDoAfter);

        InitializeAbilities();
    }

    private void OnMapInit(Entity<MorphComponent> ent, ref MapInitEvent args)
    {
        _container.EnsureContainer<Container>(ent, StomachContainer);

        foreach (var proto in new[] { ent.Comp.MimicAction, ent.Comp.AmbushAction, ent.Comp.ReproduceAction, ent.Comp.OpenVentAction, ent.Comp.PassAirlockAction }
                     .Concat(ent.Comp.ExtraActions))
        {
            if (_actions.AddAction(ent, proto) is { } action)
                ent.Comp.GrantedActions.Add(action);
        }

        var meta = MetaData(ent);
        ent.Comp.OriginalName = meta.EntityName;
        ent.Comp.OriginalDescription = meta.EntityDescription;
    }

    #region Роль

    /// <summary>make_morph_antag(): приветствие и две цели при первом заселении игрока.</summary>
    private void OnMindAdded(Entity<MorphComponent> ent, ref MindAddedMessage args)
    {
        if (ent.Comp.Greeted || !_mind.TryGetMind(ent, out var mindId, out var mind))
            return;

        ent.Comp.Greeted = true;

        if (!_roles.MindHasRole<MorphRoleComponent>(mindId))
            _roles.MindAddRole(mindId, MorphMindRole, mind, silent: true);

        foreach (var proto in new[] { EatObjective, ProcreateObjective })
        {
            if (_objectives.TryCreateObjective((mindId, mind), proto, out var objective))
                _mind.AddObjective(mindId, mind, objective.Value);
        }

        if (mind.UserId is { } user && _players.TryGetSessionById(user, out var session))
        {
            var text = Loc.GetString("morph-greeting");
            _chat.ChatMessageToOne(ChatChannel.Server, text, text, default, false, session.Channel, Color.FromHex("#c80000"));
            _audio.PlayGlobal(MutateSound, session);
        }
    }

    #endregion

    #region Голод

    /// <summary>calc_food_gained(): живое вкусно, остальное — гадость.</summary>
    public int CalcFoodGained(Entity<MorphComponent> morph, EntityUid target)
    {
        if (!HasComp<MobStateComponent>(target))
            return -morph.Comp.ItemEatCost;

        var gained = Math.Max(5, 10 * GetMobSize(target));
        if (HasComp<HumanoidProfileComponent>(target))
            gained += 10; // Люди особенно вкусные.

        return gained;
    }

    /// <summary>mob_size: TINY 0, SMALL 1, HUMAN 2, LARGE 3 — у нас по массе тела.</summary>
    private int GetMobSize(EntityUid target)
    {
        var mass = CompOrNull<PhysicsComponent>(target)?.Mass ?? 70f;
        return mass switch
        {
            < 15f => 0,
            < 50f => 1,
            <= 150f => 2,
            _ => 3,
        };
    }

    public bool UseFood(Entity<MorphComponent> morph, int amount)
    {
        if (amount > morph.Comp.GatheredFood)
            return false;

        AddFood(morph, -amount);
        return true;
    }

    public void AddFood(Entity<MorphComponent> morph, int amount)
    {
        morph.Comp.GatheredFood += amount;
        Dirty(morph);
    }

    private void ShowFoodLeft(Entity<MorphComponent> morph)
    {
        _popup.PopupEntity(Loc.GetString("morph-food-left", ("amount", morph.Comp.GatheredFood)), morph, morph);
    }

    /// <summary>try_eat(): 3 секунды хруста.</summary>
    public void TryEat(Entity<MorphComponent> morph, EntityUid target)
    {
        var food = CalcFoodGained(morph, target);
        if (food + morph.Comp.GatheredFood < 0)
        {
            _popup.PopupEntity(Loc.GetString("morph-eat-too-disgusting"), morph, morph, PopupType.SmallCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString(food < 0 ? "morph-eat-start-disgusting" : "morph-eat-start", ("target", target)), morph, morph);
        _popup.PopupEntity(Loc.GetString("morph-eat-start-others", ("morph", morph.Owner), ("target", target)), morph, Filter.PvsExcept(morph), true, PopupType.MediumCaution);

        var args = new DoAfterArgs(EntityManager, morph, morph.Comp.EatTime, new MorphEatDoAfterEvent(), morph, target: target)
        {
            BreakOnMove = true,
            NeedHand = false,
        };
        _doAfter.TryStartDoAfter(args);
    }

    private void OnEatDoAfter(Entity<MorphComponent> ent, ref MorphEatDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        if (CalcFoodGained(ent, target) + ent.Comp.GatheredFood < 0)
        {
            _popup.PopupEntity(Loc.GetString("morph-eat-too-disgusting"), ent, ent, PopupType.SmallCaution);
            return;
        }

        Eat(ent, target);
    }

    /// <summary>eat(): проглотить целиком, выключить датчики костюма, вылечиться.</summary>
    public bool Eat(Entity<MorphComponent> morph, EntityUid target)
    {
        if (!Exists(target) || _container.ContainsEntity(morph, target))
            return false;

        var stomach = _container.EnsureContainer<Container>(morph, StomachContainer);

        _popup.PopupEntity(Loc.GetString("morph-swallow", ("morph", morph.Owner), ("target", target)), morph, PopupType.MediumCaution);

        // U.turn_sensors_off()
        if (_inventory.TryGetSlotEntity(target, "jumpsuit", out var uniform) && HasComp<SuitSensorComponent>(uniform))
            _sensors.SetSensor(uniform.Value, SuitSensorMode.SensorOff);

        if (TryComp<PullableComponent>(target, out var pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(target, pullable);

        var food = CalcFoodGained(morph, target);
        if (!_container.Insert(target, stomach))
            return false;

        AddFood(morph, food);
        if (food > 0)
            _damage.HealDistributed(morph.Owner, -FixedPoint2.New(food));

        ShowFoodLeft(morph);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(morph):morph} ate {ToPrettyString(target):target}");
        return true;
    }

    /// <summary>eject_contents(): всё съеденное вываливается наружу.</summary>
    public void EjectContents(EntityUid morph)
    {
        if (!_container.TryGetContainer(morph, StomachContainer, out var stomach))
            return;

        var coords = Transform(morph).Coordinates;
        foreach (var item in stomach.ContainedEntities.ToArray())
        {
            _container.Remove(item, stomach, destination: coords);
            if (_random.Prob(0.9f))
                _xform.SetCoordinates(item, coords.Offset(_random.NextAngle().ToVec()));
        }
    }

    #endregion

    #region Атаки

    private void OnMeleeHit(Entity<MorphComponent> ent, ref MeleeHitEvent args)
    {
        // Морф бьёт сам собой — чужое оружие нас не касается.
        if (args.Weapon != ent.Owner)
            return;

        // В облике морф заметно слабее.
        if (ent.Comp.Morphed)
            args.ModifiersList.Add(ScaleAll(args.BaseDamage, ent.Comp.MorphedDamageModifier));

        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        var target = args.HitEntities[0];

        // AttackingTarget(): трупы и незакреплённые предметы съедаются.
        if (IsEdible(target))
        {
            args.Handled = true;
            TryEat(ent, target);
            return;
        }

        if (HasComp<MobStateComponent>(target) && ent.Comp.AmbushPrepared)
        {
            args.Handled = true;
            AmbushAttack(ent, target, touched: false);
            return;
        }

        // . = ..(); if(. && morphed) restore_form()
        if (ent.Comp.Morphed)
            RestoreForm(ent);
    }

    /// <summary>Без боевого режима морф тоже ест то, на что нажал.</summary>
    private void OnUserActivate(Entity<MorphComponent> ent, ref UserActivateInWorldEvent args)
    {
        if (args.Handled || !IsEdible(args.Target))
            return;

        args.Handled = true;
        TryEat(ent, args.Target);
    }

    private bool IsEdible(EntityUid target)
    {
        if (HasComp<MorphComponent>(target) && !_mobs.IsDead(target))
            return false;

        if (TryComp<MobStateComponent>(target, out var state))
            return _mobs.IsDead(target, state);

        return HasComp<ItemComponent>(target) && !Transform(target).Anchored;
    }

    /// <summary>MORPH_ATTACKED: любой удар срывает облик.</summary>
    private void OnAttacked(Entity<MorphComponent> ent, ref AttackedEvent args)
    {
        if (ent.Comp.Morphed)
            RestoreForm(ent);
    }

    /// <summary>attack_hand(): тронуть морфа в засаде — плохая идея.</summary>
    private void OnInteractHand(Entity<MorphComponent> ent, ref InteractHandEvent args)
    {
        if (!ent.Comp.AmbushPrepared || args.User == ent.Owner)
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("morph-ambush-touched-hand"), args.User, args.User, PopupType.LargeCaution);
        AmbushAttack(ent, args.User, touched: true);
    }

    /// <summary>attack_by() на help-интенте и crowbar_act() у мёртвого морфа.</summary>
    private void OnInteractUsing(Entity<MorphComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.AmbushPrepared)
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("morph-ambush-touched-item", ("item", args.Used)), args.User, args.User, PopupType.LargeCaution);
            AmbushAttack(ent, args.User, touched: true);
            return;
        }

        if (!_mobs.IsDead(ent) || !_tools.HasQuality(args.Used, "Prying"))
            return;

        args.Handled = true;
        if (!_container.TryGetContainer(ent, StomachContainer, out var stomach) || stomach.ContainedEntities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("morph-jaws-empty", ("morph", ent.Owner)), ent, args.User);
            return;
        }

        _audio.PlayPvs(SliceSound, ent);
        _popup.PopupEntity(Loc.GetString("morph-jaws-pry-start", ("user", args.User)), ent, PopupType.MediumCaution);

        var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(8), new MorphPryJawsDoAfterEvent(), ent, target: ent, used: args.Used)
        {
            BreakOnMove = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnPryJawsDoAfter(Entity<MorphComponent> ent, ref MorphPryJawsDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        EjectContents(ent);
        _audio.PlayPvs(SplatSound, ent);
    }

    /// <summary>ambush_attack(): прыжок из засады, вдвое сильнее, если цель сама его коснулась.</summary>
    public void AmbushAttack(Entity<MorphComponent> morph, EntityUid target, bool touched)
    {
        morph.Comp.AmbushPrepared = false;

        var weaken = morph.Comp.AmbushWeaken;
        var damage = morph.Comp.AmbushDamage;
        if (touched)
        {
            weaken *= 2;
            damage *= 2;
        }

        _stun.TryUpdateParalyzeDuration(target, weaken);

        var spec = new DamageSpecifier();
        spec.DamageDict["Blunt"] = FixedPoint2.New(damage);
        _damage.TryChangeDamage(target, spec, origin: morph);

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Morph/blobattack.ogg"), morph);
        _popup.PopupEntity(Loc.GetString("morph-ambush-leap", ("morph", morph.Owner), ("target", target)), morph, PopupType.LargeCaution);
        _adminLog.Add(LogType.AttackArmedClick, LogImpact.Medium, $"{ToPrettyString(morph):morph} ambush attacked {ToPrettyString(target):target}");

        RestoreForm(morph);
    }

    private static DamageModifierSet ScaleAll(DamageSpecifier damage, float coefficient)
    {
        var set = new DamageModifierSet();
        foreach (var key in damage.DamageDict.Keys)
        {
            set.Coefficients[key] = coefficient;
        }

        return set;
    }

    #endregion

    #region Засада

    /// <summary>prepare_ambush().</summary>
    public void PrepareAmbush(Entity<MorphComponent> morph)
    {
        morph.Comp.AmbushPrepared = true;
        morph.Comp.AmbushPreparedAt = _timing.CurTime;
        Dirty(morph);

        _popup.PopupEntity(Loc.GetString("morph-ambush-ready"), morph, morph, PopupType.Medium);
        _alerts.ShowAlert(morph.Owner, AmbushAlert);
    }

    /// <summary>failed_ambush().</summary>
    public void FailedAmbush(Entity<MorphComponent> morph)
    {
        morph.Comp.AmbushPrepared = false;
        morph.Comp.PerfectDisguise = false;
        Dirty(morph);

        _alerts.ClearAlert(morph.Owner, AmbushAlert);
        _alerts.ClearAlert(morph.Owner, PerfectAmbushAlert);
    }

    /// <summary>perfect_ambush(): 15 секунд неподвижности делают маскировку безупречной.</summary>
    private void PerfectAmbush(Entity<MorphComponent> morph)
    {
        morph.Comp.PerfectDisguise = true;
        Dirty(morph);

        _alerts.ClearAlert(morph.Owner, AmbushAlert);
        _alerts.ShowAlert(morph.Owner, PerfectAmbushAlert);
        _popup.PopupEntity(Loc.GetString("morph-ambush-perfect"), morph, morph, PopupType.Medium);
    }

    /// <summary>on_move(): шаг с места сбивает засаду.</summary>
    private void OnMove(Entity<MorphComponent> ent, ref MoveEvent args)
    {
        if (!ent.Comp.AmbushPrepared)
            return;

        if (args.OldPosition.EntityId == args.NewPosition.EntityId
            && (args.OldPosition.Position - args.NewPosition.Position).LengthSquared() < 0.0001f)
            return;

        FailedAmbush(ent);
        _popup.PopupEntity(Loc.GetString("morph-ambush-moved"), ent, ent, PopupType.SmallCaution);
    }

    #endregion

    #region Смерть и прочее

    private void OnMobStateChanged(Entity<MorphComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        // spell/mimic/on_death(): облик слетает, если морф не разорван.
        if (ent.Comp.Morphed)
        {
            RestoreForm(ent, showMessage: false);
            _popup.PopupEntity(Loc.GetString("morph-death-dissolve", ("morph", ent.Owner)), ent, PopupType.LargeCaution);
        }

        FailedAmbush(ent);
    }

    private void OnGibbed(Entity<MorphComponent> ent, ref BeingGibbedEvent args)
    {
        EjectContents(ent);
    }

    private void OnExamined(Entity<MorphComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Morphed && !ent.Comp.PerfectDisguise && args.Examiner != ent.Owner
            && _xform.InRange(args.Examiner, ent.Owner, WrongnessRange))
        {
            args.PushMarkup(Loc.GetString("morph-examine-wrong"));
        }

        if (args.Examiner == ent.Owner)
            args.PushMarkup(Loc.GetString("morph-examine-food", ("amount", ent.Comp.GatheredFood)));

        if (_mobs.IsDead(ent) && args.IsInDetailsRange)
        {
            var hasContents = _container.TryGetContainer(ent, StomachContainer, out var stomach) && stomach.ContainedEntities.Count > 0;
            args.PushMarkup(Loc.GetString(hasContents ? "morph-examine-dead-contents" : "morph-examine-dead-empty", ("morph", ent.Owner)));
        }
    }

    private void OnRefreshSpeed(Entity<MorphComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Morphed)
            args.ModifySpeed(ent.Comp.MorphedSpeedModifier, ent.Comp.MorphedSpeedModifier);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<MorphComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.AmbushPrepared && !comp.PerfectDisguise && now >= comp.AmbushPreparedAt + comp.AmbushPerfectionTime)
                PerfectAmbush((uid, comp));

            UpdateAi((uid, comp), now);
        }
    }

    #endregion
}

/// <summary>Роль морфа на сущности роли разума.</summary>
[RegisterComponent]
public sealed partial class MorphRoleComponent : Component;
