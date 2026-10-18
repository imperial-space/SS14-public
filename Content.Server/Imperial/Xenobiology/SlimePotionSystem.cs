using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Speech.Components;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Humanoid;
using Content.Shared.Atmos;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Xenobiology;

/// <summary>
/// obj/item/slimepotion: зелья из экстрактов слаймов.
/// </summary>
public sealed class SlimePotionSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly GrammarSystem _grammar = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly QuickDialogSystem _dialog = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SlimeSystem _slime = default!;

    private static readonly ProtoId<DamageTypePrototype> Asphyxiation = "Asphyxiation";
    private static readonly ProtoId<RadioChannelPrototype> CommonChannel = "Common";
    private static readonly ProtoId<RadioChannelPrototype> ScienceChannel = "Science";

    /// <summary>Подтверждение переноса сознания: повторное применение в течение этого времени.</summary>
    private static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(5);

    private readonly Dictionary<EntityUid, (EntityUid Target, TimeSpan Until)> _transferConfirm = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimePotionComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SlimePotionComponent, SlimeGenderChangeDoAfterEvent>(OnGenderDoAfter);
        SubscribeLocalEvent<SlimePotionedComponent, InventoryRelayedEvent<GetFireProtectionEvent>>(OnGetFireProtection);
    }

    private void OnGetFireProtection(Entity<SlimePotionedComponent> ent, ref InventoryRelayedEvent<GetFireProtectionEvent> args)
    {
        if (ent.Comp.Fireproof || ent.Comp.Lavaproof)
            args.Args.Reduce(FireproofReduction(ent));
    }

    /// <summary>max_heat_protection_temperature = FIRE_IMMUNITY: защищается вся покрытая часть тела (доли heat_protection SS13).</summary>
    private float FireproofReduction(EntityUid clothing)
    {
        if (!TryComp<ClothingComponent>(clothing, out var cloth))
            return 0f;

        var slots = cloth.Slots;
        var reduction = 0f;
        if ((slots & SlotFlags.HEAD) != 0)
            reduction += 0.3f;
        if ((slots & SlotFlags.OUTERCLOTHING) != 0 || (slots & SlotFlags.INNERCLOTHING) != 0)
            reduction += 0.6f;
        if ((slots & SlotFlags.GLOVES) != 0)
            reduction += 0.05f;
        if ((slots & SlotFlags.FEET) != 0)
            reduction += 0.05f;
        return reduction;
    }

    private void OnAfterInteract(Entity<SlimePotionComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        // slimepotion/interact_with_atom(): в контейнер реагентов зелье не переливается.
        if (HasComp<SolutionContainerManagerComponent>(target) && !HasComp<MobStateComponent>(target) && ent.Comp.Kind is not (SlimePotionKind.Enhancer or SlimePotionKind.MaxEnhancer))
        {
            _popup.PopupEntity(Loc.GetString("slime-potion-not-container", ("potion", ent.Owner), ("target", target)), args.User, args.User);
            args.Handled = true;
            return;
        }

        if (ent.Comp.SlimeOnly && !HasComp<SlimeComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("slime-potion-slime-only", ("potion", ent.Owner)), args.User, args.User);
            return;
        }

        args.Handled = Apply(ent, target, args.User);
    }

    /// <summary>Применяет зелье к цели (также из консоли ксенобиологии).</summary>
    public bool Apply(Entity<SlimePotionComponent> ent, EntityUid target, EntityUid user)
    {
        return ent.Comp.Kind switch
        {
            SlimePotionKind.Docility => Docility(ent, target, user),
            SlimePotionKind.Sentience => Sentience(ent, target, user),
            SlimePotionKind.Transference => Transference(ent, target, user),
            SlimePotionKind.Steroid => Steroid(ent, target, user),
            SlimePotionKind.Enhancer or SlimePotionKind.MaxEnhancer => Enhancer(ent, target, user),
            SlimePotionKind.Stabilizer => Stabilizer(ent, target, user),
            SlimePotionKind.Mutator => Mutator(ent, target, user),
            SlimePotionKind.Speed => Speed(ent, target, user),
            SlimePotionKind.Fireproof => Fireproof(ent, target, user),
            SlimePotionKind.GenderChange => GenderChange(ent, target, user),
            SlimePotionKind.Renaming => Renaming(ent, target, user),
            SlimePotionKind.Radio => Radio(ent, target, user),
            _ => false,
        };
    }

    private void Popup(string key, EntityUid user, params (string, object)[] args)
    {
        _popup.PopupEntity(Loc.GetString(key, args), user, user);
    }

    private void Consume(Entity<SlimePotionComponent> ent)
    {
        QueueDel(ent);
    }

    /// <summary>Мобы без разума: у них нет игрока (ckey).</summary>
    private bool HasPlayer(EntityUid uid)
    {
        return TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind;
    }

    /// <summary>compare_sentience_type(SENTIENCE_ORGANIC): простые органические существа.</summary>
    private bool IsOrganicAnimal(EntityUid uid)
    {
        return HasComp<MobStateComponent>(uid)
               && !HasComp<HumanoidProfileComponent>(uid)
               && !HasComp<BorgChassisComponent>(uid);
    }

    #region Слаймовые зелья

    private bool Docility(Entity<SlimePotionComponent> ent, EntityUid target, EntityUid user)
    {
        if (!TryComp<SlimeComponent>(target, out var slime))
            return false;
        if (_mobState.IsDead(target))
        {
            Popup("slime-potion-slime-dead", user);
            return true;
        }

        if (slime.Rabid)
        {
            // Бешенство проходит, но слайм не становится по-настоящему ручным.
            _slime.SetRabid((target, slime), false);
            _slime.SetDefault((target, slime));
            Popup("slime-potion-docility-rabid-user", user);
            Popup("slime-potion-docility-rabid-slime", target);
            Consume(ent);
            return true;
        }

        _slime.SetPacified((target, slime));
        Popup("slime-potion-docility-slime", target);
        Popup("slime-potion-docility-user", user);
        Consume(ent);

        var defaultName = Loc.GetString("slime-potion-docility-default-name");
        if (!TryComp<ActorComponent>(user, out var actor))
        {
            _slime.SetCustomName(target, defaultName);
            return true;
        }

        _slime.SetCustomName(target, defaultName);
        _dialog.OpenDialog<string>(actor.PlayerSession,
            Loc.GetString("slime-potion-docility-title"),
            Loc.GetString("slime-potion-docility-prompt"),
            name =>
            {
                if (TerminatingOrDeleted(target))
                    return;
                name = name.Trim();
                if (name.Length > 32)
                    name = name[..32];
                _slime.SetCustomName(target, string.IsNullOrWhiteSpace(name) ? defaultName : name);
            });
        return true;
    }

    private bool Steroid(Entity<SlimePotionComponent> ent, EntityUid target, EntityUid user)
    {
        if (!TryComp<SlimeComponent>(target, out var slime))
            return false;
        if (slime.Stage == SlimeLifeStage.Adult)
        {
            Popup("slime-potion-steroid-adult", user);
            return true;
        }

        if (_mobState.IsDead(target))
        {
            Popup("slime-potion-slime-dead", user);
            return true;
        }

        if (slime.Cores >= 5)
        {
            Popup("slime-potion-steroid-max", user);
            return true;
        }

        slime.Cores++;
        Dirty(target, slime);
        Popup("slime-potion-steroid", user);
        Consume(ent);
        return true;
    }

    private bool Stabilizer(Entity<SlimePotionComponent> ent, EntityUid target, EntityUid user)
    {
        if (!TryComp<SlimeComponent>(target, out var slime))
            return false;
        if (_mobState.IsDead(target))
        {
            Popup("slime-potion-slime-dead", user);
            return true;
        }

        if (slime.MutationChance <= 0)
        {
            Popup("slime-potion-stabilizer-max", user);
            return true;
        }

        slime.MutationChance = Math.Clamp(slime.MutationChance - 15, 0, 100);
        Dirty(target, slime);
        Popup("slime-potion-stabilizer", user);
        Consume(ent);
        return true;
    }

    private bool Mutator(Entity<SlimePotionComponent> ent, EntityUid target, EntityUid user)
    {
        if (!TryComp<SlimeComponent>(target, out var slime))
            return false;
        if (_mobState.IsDead(target))
        {
            Popup("slime-potion-slime-dead", user);
            return true;
        }

        if (slime.MutatorUsed)
        {
            Popup("slime-potion-mutator-used", user);
            return true;
        }

        if (slime.MutationChance >= 100)
        {
            Popup("slime-potion-mutator-max", user);
            return true;
        }

        slime.MutationChance = Math.Clamp(slime.MutationChance + 12, 0, 100);
        slime.MutatorUsed = true;
        Dirty(target, slime);
        Popup("slime-potion-mutator", user);
        Consume(ent);
        return true;
    }

    #endregion

    #region Экстракты

    /// <summary>slime_extract/item_interaction(): усилитель добавляет заряд, максимайзер — до пяти.</summary>
    private bool Enhancer(Entity<SlimePotionComponent> ent, EntityUid target, EntityUid user)
    {
        if (!TryComp<SlimeExtractComponent>(target, out var extract))
            return false;

        if (extract.Uses >= 5 || extract.Recurring)
        {
            Popup("slime-potion-enhancer-max", user);
            return true;
        }

        if (ent.Comp.Kind == SlimePotionKind.MaxEnhancer)
        {
            extract.Uses = 5;
            Popup("slime-potion-enhancer-maximized", user);
        }
        else
        {
            extract.Uses++;
            Popup("slime-potion-enhancer", user);
        }

        Dirty(target, extract);
        Consume(ent);
        return true;
    }

    #endregion

    #region Разум

    private bool Sentience(Entity<SlimePotionComponent> ent, EntityUid target, EntityUid user)
    {
        if (!HasComp<MobStateComponent>(target))
            return false;
        if (ent.Comp.InUse)
            return true;
        if (HasPlayer(target) || HasComp<GhostRoleComponent>(target))
        {
            Popup("slime-potion-already-sentient", user);
            return true;
        }

        if (_mobState.IsDead(target))
        {
            Popup("slime-potion-dead", user);
            return true;
        }

        if (!IsOrganicAnimal(target))
        {
            Popup("slime-potion-invalid", user);
            return true;
        }

        ent.Comp.InUse = true;
        RemComp<ReplacementAccentComponent>(target);
        var ghostRole = AddComp<GhostRoleComponent>(target);
        EnsureComp<GhostTakeoverAvailableComponent>(target);
        ghostRole.RoleName = Name(target);
        ghostRole.RoleDescription = Loc.GetString("slime-potion-sentience-role-description");
        ghostRole.RoleRules = Loc.GetString("slime-potion-sentience-role-rules", ("creator", Name(user)));

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user):user} применил зелье разума на {ToPrettyString(target):target}");
        Popup("slime-potion-sentience-offered", user);
        Consume(ent);
        return true;
    }

    /// <summary>slimepotion/transference: сознание переходит в существо, прежнее тело умирает.</summary>
    private bool Transference(Entity<SlimePotionComponent> ent, EntityUid target, EntityUid user)
    {
        if (!HasComp<MobStateComponent>(target) || target == user)
            return false;
        if (HasPlayer(target))
        {
            Popup("slime-potion-already-sentient", user);
            return true;
        }

        if (_mobState.IsDead(target))
        {
            Popup("slime-potion-dead", user);
            return true;
        }

        if (!IsOrganicAnimal(target))
        {
            Popup("slime-potion-invalid", user);
            return true;
        }

        if (!_mind.TryGetMind(user, out var mindId, out var mind))
            return true;

        // tgui_alert «Вы уверены?»: подтверждение повторным применением.
        var now = _timing.CurTime;
        if (!_transferConfirm.TryGetValue(user, out var confirm) || confirm.Target != target || now > confirm.Until)
        {
            _transferConfirm[user] = (target, now + ConfirmWindow);
            Popup("slime-potion-transference-confirm", user, ("target", target));
            return true;
        }

        _transferConfirm.Remove(user);
        Popup("slime-potion-transference-drink", user, ("target", target));

        var userName = Name(user);
        _mind.TransferTo(mindId, target, mind: mind);

        // SET_FACTION_AND_ALLIES_FROM
        if (TryComp<NpcFactionMemberComponent>(user, out var userFactions))
        {
            _faction.ClearFactions(target, dirty: false);
            _faction.AddFactions(target, userFactions.Factions);
        }

        RemComp<ReplacementAccentComponent>(target);
        RemComp<GhostRoleComponent>(target);
        RemComp<GhostTakeoverAvailableComponent>(target);
        _meta.SetEntityName(target, userName);

        // user.death()
        if (_thresholds.TryGetDeadThreshold(user, out var dead) && TryComp<DamageableComponent>(user, out var damageable))
        {
            var need = dead.Value - _damageable.GetTotalDamage((user, damageable));
            if (need > 0)
                _damageable.TryChangeDamage(user, new DamageSpecifier(_proto.Index(Asphyxiation), need), ignoreResistances: true);
        }
        _mobState.ChangeMobState(user, MobState.Dead);

        Popup("slime-potion-transference-done", target, ("target", target));
        _adminLog.Add(LogType.Mind, LogImpact.High, $"{ToPrettyString(user):user} перенёс сознание в {ToPrettyString(target):target} зельем переноса");
        Consume(ent);
        return true;
    }

    /// <summary>slimepotion/renaming: самоосознающее существо выбирает себе новое имя.</summary>
    private bool Renaming(Entity<SlimePotionComponent> ent, EntityUid target, EntityUid user)
    {
        if (!HasComp<MobStateComponent>(target))
            return false;
        if (ent.Comp.InUse)
            return true;
        if (!TryComp<ActorComponent>(target, out var actor))
        {
            Popup("slime-potion-renaming-not-aware", user, ("target", target));
            return true;
        }

        ent.Comp.InUse = true;
        Popup("slime-potion-renaming-offer", user, ("target", target));
        var oldName = Name(target);
        _dialog.OpenDialog<string>(actor.PlayerSession,
            Loc.GetString("slime-potion-renaming-title"),
            Loc.GetString("slime-potion-renaming-prompt"),
            name =>
            {
                ent.Comp.InUse = false;
                name = name.Trim();
                if (name.Length > 32)
                    name = name[..32];
                if (TerminatingOrDeleted(ent) || TerminatingOrDeleted(target) || string.IsNullOrWhiteSpace(name) || name == oldName)
                    return;

                _meta.SetEntityName(target, name);
                _popup.PopupEntity(Loc.GetString("slime-potion-renaming-done", ("old", oldName), ("new", name)), target, PopupType.Medium);
                _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(user):user} использовал зелье переименования на {ToPrettyString(target):target}, новое имя {name}");
                Consume(ent);
            },
            () => ent.Comp.InUse = false);
        return true;
    }

    /// <summary>slimepotion/slimeradio: имплант-радио слаймов (ключ научного отдела).</summary>
    private bool Radio(Entity<SlimePotionComponent> ent, EntityUid target, EntityUid user)
    {
        if (!HasComp<MobStateComponent>(target))
            return false;
        if (!IsOrganicAnimal(target))
        {
            Popup("slime-potion-radio-complex", user, ("target", target));
            return true;
        }

        if (_mobState.IsDead(target))
        {
            Popup("slime-potion-dead", user);
            return true;
        }

        EnsureComp<IntrinsicRadioReceiverComponent>(target);
        var transmitter = EnsureComp<IntrinsicRadioTransmitterComponent>(target);
        transmitter.Channels.Add(CommonChannel);
        transmitter.Channels.Add(ScienceChannel);
        var active = EnsureComp<ActiveRadioComponent>(target);
        active.Channels.Add(CommonChannel);
        active.Channels.Add(ScienceChannel);

        Popup("slime-potion-radio", user, ("target", target));
        Popup("slime-potion-radio-target", target);
        Consume(ent);
        return true;
    }

    #endregion

    #region Предметы

    /// <summary>slimepotion/speed: убирает замедление предмета.</summary>
    private bool Speed(Entity<SlimePotionComponent> ent, EntityUid target, EntityUid user)
    {
        if (HasComp<MobStateComponent>(target))
        {
            Popup("slime-potion-objects-only", user);
            return false;
        }

        if (TryComp<SlimePotionedComponent>(target, out var potioned) && potioned.Speed)
        {
            Popup("slime-potion-speed-cant", user, ("target", target));
            return true;
        }

        var slowClothing = TryComp<ClothingSpeedModifierComponent>(target, out var clothingSpeed)
                           && (clothingSpeed.WalkModifier < 1f || clothingSpeed.SprintModifier < 1f);
        var slowHeld = TryComp<HeldSpeedModifierComponent>(target, out var heldSpeed)
                       && (heldSpeed.WalkModifier < 1f || heldSpeed.SprintModifier < 1f);
        if (!slowClothing && !slowHeld)
        {
            // Предмет-хранилище: пусть зелье просто положится внутрь.
            if (HasComp<Content.Shared.Storage.StorageComponent>(target))
                return false;
            Popup("slime-potion-speed-cant", user, ("target", target));
            return true;
        }

        if (slowClothing)
            RemComp<ClothingSpeedModifierComponent>(target);
        if (slowHeld)
            RemComp<HeldSpeedModifierComponent>(target);

        var wearer = Transform(target).ParentUid;
        if (wearer.IsValid())
            _speed.RefreshMovementSpeedModifiers(wearer);

        potioned = EnsureComp<SlimePotionedComponent>(target);
        potioned.Speed = true;
        Dirty(target, potioned);
        Popup("slime-potion-speed", user, ("target", target));
        Consume(ent);
        return true;
    }

    /// <summary>slimepotion/fireproof: огнеупорность одежды, три применения.</summary>
    private bool Fireproof(Entity<SlimePotionComponent> ent, EntityUid target, EntityUid user)
    {
        if (ent.Comp.Uses <= 0)
        {
            Consume(ent);
            return true;
        }

        if (!HasComp<ClothingComponent>(target))
        {
            Popup("slime-potion-clothing-only", user);
            return false;
        }

        var already = TryComp<SlimePotionedComponent>(target, out var potioned) && potioned.Fireproof
                      || TryComp<FireProtectionComponent>(target, out var protection) && protection.Reduction >= FireproofReduction(target);
        if (already)
        {
            Popup("slime-potion-fireproof-already", user, ("target", target));
            return true;
        }

        potioned = EnsureComp<SlimePotionedComponent>(target);
        potioned.Fireproof = true;
        Dirty(target, potioned);
        _meta.SetEntityName(target, Loc.GetString("slime-potion-fireproofed-name", ("name", Name(target))));
        Popup("slime-potion-fireproof", user, ("target", target));

        ent.Comp.Uses--;
        Dirty(ent);
        if (ent.Comp.Uses <= 0)
            Consume(ent);
        return true;
    }

    /// <summary>slimepotion/genderchange.</summary>
    private bool GenderChange(Entity<SlimePotionComponent> ent, EntityUid target, EntityUid user)
    {
        if (!HasComp<MobStateComponent>(target))
            return false;
        if (_mobState.IsDead(target))
        {
            Popup("slime-potion-living-only", user);
            return true;
        }

        if (!TryComp<GrammarComponent>(target, out var grammar) || grammar.Gender is not (Gender.Male or Gender.Female))
        {
            Popup("slime-potion-gender-only", user);
            return true;
        }

        if (HasPlayer(target))
        {
            _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(5), new SlimeGenderChangeDoAfterEvent(), ent, target, ent)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            });
            return true;
        }

        ChangeGender(ent, (target, grammar));
        return true;
    }

    private void OnGenderDoAfter(Entity<SlimePotionComponent> ent, ref SlimeGenderChangeDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !TryComp<GrammarComponent>(target, out var grammar))
            return;
        args.Handled = true;
        ChangeGender(ent, (target, grammar));
    }

    private void ChangeGender(Entity<SlimePotionComponent> ent, Entity<GrammarComponent> target)
    {
        var female = target.Comp.Gender == Gender.Male;
        _grammar.SetGender(target, female ? Gender.Female : Gender.Male);
        _popup.PopupEntity(Loc.GetString(female ? "slime-potion-gender-female" : "slime-potion-gender-male", ("target", target.Owner)),
            target, PopupType.Medium);
        Consume(ent);
    }

    #endregion
}
