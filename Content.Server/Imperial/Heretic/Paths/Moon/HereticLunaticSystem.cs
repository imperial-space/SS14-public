using Content.Server.Imperial.Heretic;
using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Imperial.Heretic.Items;
using Content.Server.Imperial.Heretic.Paths.Moon;
using Content.Server.Imperial.Heretic.Paths.Moon.Parade;
using Content.Server.Imperial.Heretic.Paths.Void;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Server.Imperial.Antimagic;
using Content.Server.Mind;
using Content.Server.Objectives;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Objectives.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Heretic.Paths.Moon;

/// <summary>
/// Лунатики 1 в 1 с BandaStation (antagonists/heretic/moon_lunatic.dm):
/// аколиты вознёсшегося еретика Луны с хозяином, целью, Хваткой Мансуса и «Эхом Лунного света».
/// </summary>
public sealed class HereticLunaticSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly ObjectivesSystem _objectives = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly ImperialAntimagicSystem _antimagic = default!;

    private static readonly EntProtoId LunaticMindRole = "MindRoleLunatic";
    private static readonly EntProtoId MasterMindRole = "MindRoleLunaticMaster";
    private static readonly EntProtoId LunaticObjective = "LunaticObjective";
    private static readonly EntProtoId MasterObjective = "LunaticMasterObjective";
    private static readonly EntProtoId TrackAction = "ActionLunaticTrack";
    private static readonly EntProtoId GraspAction = "ActionLunaticMansusGrasp";
    private static readonly EntProtoId GraspItem = "LunaticMansusGraspItem";
    private static readonly EntProtoId SlurredEffect = "StatusEffectSlurred";
    private static readonly EntProtoId AmuletProto = "HereticMoonAmulet";
    private static readonly SoundPathSpecifier LaughSound = new("/Audio/Voice/Human/manlaugh1.ogg");

    [Dependency] private readonly Content.Shared.Inventory.InventorySystem _inventory = default!;

    /// <summary>add_faction(FACTION_HERETIC): призванные твари еретика своих не трогают.</summary>
    private static readonly string HereticFaction = "HereticFamiliar";

    private static readonly SoundPathSpecifier TrackSound = new("/Audio/Imperial/heretic/sound_effects_singlebeat.ogg");
    private static readonly SoundPathSpecifier GraspSound = new("/Audio/Imperial/heretic/sound_effects_curse.ogg");

    /// <summary>cooldown_time Хватки Мансуса.</summary>
    private static readonly TimeSpan GraspCooldown = TimeSpan.FromSeconds(10);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LunaticComponent, ComponentShutdown>(OnLunaticShutdown);
        SubscribeLocalEvent<LunaticComponent, LunaticTrackActionEvent>(OnTrack);
        SubscribeLocalEvent<LunaticComponent, LunaticMansusGraspActionEvent>(OnGrasp);
        SubscribeLocalEvent<LunaticMansusGraspItemComponent, AfterInteractEvent>(OnGraspHit);
        SubscribeLocalEvent<LunaticObjectiveConditionComponent, ObjectiveGetProgressEvent>(OnObjectiveProgress);
    }

    #region Обращение

    /// <summary>
    /// ultimate/moon_final/attempt_conversion(): обратить цель в лунатика вознёсшегося еретика.
    /// Возвращает true только для нового лунатика.
    /// </summary>
    public bool TryMakeLunatic(EntityUid target, EntityUid master)
    {
        // Еретики и их твари уже служат Мансусу.
        if (HasComp<HereticComponent>(target) || HasComp<LunaticMasterComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("lunatic-convert-heretic", ("name", Name(master))), target, target, PopupType.LargeCaution);
            return false;
        }

        if (HasComp<LunaticComponent>(target))
            return false;

        // TRAIT_UNCONVERTABLE и антимагия защищают разум.
        if (HasComp<Content.Shared.Mindshield.Components.MindShieldComponent>(target) || _antimagic.CanBlockMagic(target))
        {
            _popup.PopupEntity(Loc.GetString("lunatic-convert-protected"), target, target, PopupType.MediumCaution);
            return false;
        }

        if (!_mind.TryGetMind(target, out var mindId, out var mind))
            return false;

        var lunatic = EnsureComp<LunaticComponent>(target);
        lunatic.Master = master;
        Dirty(target, lunatic);

        _roles.MindAddRole(mindId, LunaticMindRole, mind, silent: true);
        _faction.AddFaction(target, HereticFaction);

        GiveObjective(mindId, mind, LunaticObjective, Loc.GetString("lunatic-objective-title", ("master", Name(master))));

        lunatic.TrackAction = _actions.AddAction(target, TrackAction);
        lunatic.GraspAction = _actions.AddAction(target, GraspAction);

        // set_master(): «Разрушьте ложь, спасите правду…»
        SendMessage(mind, Loc.GetString("lunatic-greeting", ("master", Name(master))));
        _adminLog.Add(LogType.Mind, LogImpact.High, $"{ToPrettyString(target):player} has become a Lunatic of {ToPrettyString(master):player}");
        return true;
    }

    /// <summary>
    /// attempt_conversion() целиком: лунатик получает свой лунный амулет и заходится смехом.
    /// </summary>
    public bool TryConvertWithAmulet(EntityUid target, EntityUid master)
    {
        if (!TryMakeLunatic(target, master))
            return false;

        // equip_in_one_of_slots(neck, hands, pockets, backpack)
        var amulet = Spawn(AmuletProto, Transform(target).Coordinates);
        if (!_inventory.TryEquip(target, amulet, "neck", silent: true)
            && !_hands.TryPickupAnyHand(target, amulet)
            && !_inventory.TryEquip(target, amulet, "pocket1", silent: true)
            && !_inventory.TryEquip(target, amulet, "pocket2", silent: true))
        {
            _xform.SetCoordinates(amulet, Transform(target).Coordinates);
        }

        _audio.PlayPvs(LaughSound, target);
        return true;
    }

    /// <summary>add_antag_datum(/datum/antagonist/lunatic/master): еретик становится Постановщиком.</summary>
    public void MakeMaster(EntityUid heretic)
    {
        if (HasComp<LunaticMasterComponent>(heretic))
            return;

        EnsureComp<LunaticMasterComponent>(heretic);

        if (!_mind.TryGetMind(heretic, out var mindId, out var mind))
            return;

        _roles.MindAddRole(mindId, MasterMindRole, mind, silent: true);
        GiveObjective(mindId, mind, MasterObjective, null);
    }

    private void GiveObjective(EntityUid mindId, MindComponent mind, EntProtoId proto, string? title)
    {
        if (!_objectives.TryCreateObjective((mindId, mind), proto, out var objective))
            return;

        if (title != null)
            _metaData.SetEntityName(objective.Value, title);

        _mind.AddObjective(mindId, mind, objective.Value);
    }

    /// <summary>on_removal(): способности уходят вместе с ролью.</summary>
    private void OnLunaticShutdown(Entity<LunaticComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.TrackAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.GraspAction);
    }

    /// <summary>objective/lunatic: цель держится, пока жив Постановщик.</summary>
    private void OnObjectiveProgress(Entity<LunaticObjectiveConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        if (ent.Comp.IsMaster)
        {
            args.Progress = 1f;
            return;
        }

        var body = args.Mind.OwnedEntity;
        if (body == null || !TryComp<LunaticComponent>(body, out var lunatic) || lunatic.Master is not { } master)
        {
            args.Progress = 0f;
            return;
        }

        args.Progress = Exists(master) && !_mobs.IsDead(master) ? 1f : 0f;
    }

    #endregion

    #region Эхо Лунного света

    /// <summary>lunatic_track/Activate(): где сейчас Постановщик.</summary>
    private void OnTrack(Entity<LunaticComponent> ent, ref LunaticTrackActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        if (ent.Comp.Master is not { } master || !Exists(master))
        {
            _popup.PopupEntity(Loc.GetString("lunatic-track-no-master"), ent, ent, PopupType.MediumCaution);
            _actions.SetCooldown(args.Action.Owner, TimeSpan.FromSeconds(1));
            return;
        }

        _audio.PlayPvs(TrackSound, ent);

        var message = GetTrackMessage(ent, master);
        if (_mobs.IsDead(master))
            message = Loc.GetString("lunatic-track-dead", ("message", message));

        _popup.PopupEntity(message, ent, ent, PopupType.Medium);

        if (_mobs.IsDead(master) && _mind.TryGetMind(ent, out _, out var mind))
            SendMessage(mind, Loc.GetString("lunatic-track-master-dead", ("master", Name(master))));
    }

    /// <summary>generate_balloon_message(): расстояние и сторона света до Постановщика.</summary>
    private string GetTrackMessage(EntityUid user, EntityUid master)
    {
        var ourXform = Transform(user);
        var theirXform = Transform(master);

        if (ourXform.MapID != theirXform.MapID)
            return Loc.GetString("lunatic-track-other-plane");

        var delta = _xform.GetWorldPosition(theirXform) - _xform.GetWorldPosition(ourXform);
        var dist = delta.Length();
        var dir = Loc.GetString($"lunatic-track-dir-{delta.ToWorldAngle().GetDir().ToString().ToLowerInvariant()}");

        return dist switch
        {
            <= 15f => Loc.GetString("lunatic-track-very-close", ("dir", dir)),
            <= 31f => Loc.GetString("lunatic-track-close", ("dir", dir)),
            <= 127f => Loc.GetString("lunatic-track-far", ("dir", dir)),
            _ => Loc.GetString("lunatic-track-very-far"),
        };
    }

    #endregion

    #region Хватка Мансуса

    private void OnGrasp(Entity<LunaticComponent> ent, ref LunaticMansusGraspActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        // Повторное нажатие убирает Хватку из руки.
        foreach (var held in _hands.EnumerateHeld(ent.Owner))
        {
            if (!HasComp<LunaticMansusGraspItemComponent>(held))
                continue;

            RemComp<UnremoveableComponent>(held);
            QueueDel(held);
            return;
        }

        var item = Spawn(GraspItem, Transform(ent).Coordinates);
        if (!_hands.TryPickupAnyHand(ent.Owner, item))
        {
            QueueDel(item);
            _popup.PopupEntity(Loc.GetString("heretic-grasp-hands-full"), ent, ent);
        }
    }

    /// <summary>mansus_grasp/cast_on_hand_hit(): базовая Хватка без знаний пути.</summary>
    private void OnGraspHit(Entity<LunaticMansusGraspItemComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User)
            return;

        if (!TryComp<LunaticComponent>(args.User, out var lunatic) || !HasComp<MobStateComponent>(target))
            return;

        args.Handled = true;

        if (_antimagic.CanBlockMagic(target))
        {
            _popup.PopupEntity(Loc.GetString("lunatic-grasp-bounce", ("target", Name(target))), target, PopupType.MediumCaution);
            ConsumeGrasp(ent, lunatic);
            return;
        }

        _audio.PlayPvs(GraspSound, target);

        // apply_damage(10, BRUTE)
        var brute = new DamageSpecifier();
        brute.DamageDict["Blunt"] = FixedPoint2.New(10);
        _damage.TryChangeDamage(target, brute, origin: args.User);

        if (HasComp<HumanoidProfileComponent>(target))
        {
            if (HasComp<CultistComponent>(target))
            {
                // Кровавая магия культиста поглощает большую часть эффекта.
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(0.5), true);
                _popup.PopupEntity(Loc.GetString("lunatic-grasp-cult-absorb"), target, PopupType.MediumCaution);
            }
            else
            {
                _statusEffects.TryAddStatusEffectDuration(target, SlurredEffect, TimeSpan.FromSeconds(4));
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(5), true);
                _stamina.TakeStaminaDamage(target, 80f, source: args.User);
            }
        }

        ConsumeGrasp(ent, lunatic);
    }

    private void ConsumeGrasp(Entity<LunaticMansusGraspItemComponent> item, LunaticComponent lunatic)
    {
        if (lunatic.GraspAction is { } action)
            _actions.SetCooldown(action, GraspCooldown);

        RemComp<UnremoveableComponent>(item);
        QueueDel(item);
    }

    #endregion

    private void SendMessage(MindComponent mind, string text)
    {
        if (mind.UserId == null || !_players.TryGetSessionById(mind.UserId.Value, out var session))
            return;

        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", text));
        _chat.ChatMessageToOne(ChatChannel.Server, text, wrapped, default, false, session.Channel, Color.FromHex("#90CAF9"));
    }
}
