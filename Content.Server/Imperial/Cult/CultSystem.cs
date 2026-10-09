using System.Linq;
using Content.Server.Actions;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Imperial.Cult.Components;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared.Alert;
using Content.Shared.Body;
using Content.Shared.Damage.Systems;
using Content.Shared.Ghost;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cult;

/// <summary>
/// Культ крови Нар'Си: культисты, команда, общение и способности мастера (datum/antagonist/cult, datum/team/cult).
/// </summary>
public sealed partial class CultSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedVisualBodySystem _visualBody = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ImperialCultRuleSystem _rule = default!;
    [Dependency] private readonly CultBloodMagicSystem _magic = default!;
    [Dependency] private readonly CultStructureSystem _structures = default!;
    [Dependency] private readonly CultRuneSystem _runes = default!;

    public const string CultFaction = "Cult";
    public static readonly EntProtoId MindRole = "MindRoleCultist";
    private static readonly EntProtoId CommunionAction = "ActionCultCommunion";
    private static readonly EntProtoId PrepareMagicAction = "ActionCultPrepareBloodMagic";
    private static readonly EntProtoId ReckoningAction = "ActionCultFinalReckoning";
    private static readonly EntProtoId MarkAction = "ActionCultMarkTarget";
    private static readonly EntProtoId PulseAction = "ActionCultEldritchPulse";
    private static readonly EntProtoId PassMantleAction = "ActionCultPassMantle";
    private static readonly ProtoId<AlertPrototype> BloodSenseAlert = "CultBloodSense";
    public static readonly Color CultRed = Color.FromHex("#960000");

    /// <summary>Исходные цвета глаз до cult_eyes.</summary>
    private readonly Dictionary<EntityUid, Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData>> _originalEyes = new();

    private TimeSpan _nextSenseUpdate;

    public override void Initialize()
    {
        base.Initialize();
        InitializeMenus();
        InitializeLeader();
        InitializeMisc();
        InitializeBody();

        SubscribeLocalEvent<CultistComponent, ComponentShutdown>(OnCultistShutdown);
        SubscribeLocalEvent<CultistComponent, CultCommunionActionEvent>(OnCommunion);
        SubscribeLocalEvent<CultistComponent, CultCommuneActionEvent>(OnLegacyCommune);
        SubscribeLocalEvent<CultistComponent, MobStateChangedEvent>(OnCultistMobState);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        UpdateBloodTarget(now);
        UpdateVisions(now);

        if (now < _nextSenseUpdate)
            return;
        _nextSenseUpdate = now + TimeSpan.FromSeconds(1);
        UpdateBloodSense();
    }

    #region Принадлежность

    public bool IsCultist(EntityUid uid) => HasComp<CultistComponent>(uid);

    /// <summary>IS_CULTIST_OR_CULTIST_MOB: культист, конструкт культа или тень культа.</summary>
    public bool IsCultAligned(EntityUid uid) =>
        HasComp<CultistComponent>(uid) || HasComp<CultConstructComponent>(uid) || HasComp<CultShadeComponent>(uid);

    /// <summary>is_convertable_to_cult().</summary>
    public bool IsConvertable(EntityUid target)
    {
        if (!_mind.TryGetMind(target, out var mindId, out _))
            return false;
        if (!HasComp<ActorComponent>(target))
            return false;
        if (HasComp<HumanoidProfileComponent>(target) && CultForeignComponents.Has(EntityManager, target, CultForeignComponents.Holy))
            return false;
        if (_rule.IsSacrificeTarget(mindId))
            return false;
        if (CultForeignComponents.Has(EntityManager, target, CultForeignComponents.Heretic))
            return false;
        // TRAIT_UNCONVERTABLE (майндщит), силиконы.
        if (HasComp<MindShieldComponent>(target) || HasComp<BorgChassisComponent>(target))
            return false;
        return true;
    }

    /// <summary>datum/antagonist/cult/on_gain() + apply_innate_effects().</summary>
    public void AddCultist(EntityUid uid, bool converted = false, bool silent = false, bool equip = false, bool cultGhost = false)
    {
        if (HasComp<CultistComponent>(uid))
            return;

        var rule = _rule.EnsureRule();
        var cultist = EnsureComp<CultistComponent>(uid);
        cultist.Converted = converted;
        cultist.Silent = silent;
        cultist.CultGhost = cultGhost;

        _faction.AddFaction(uid, CultFaction);
        _actions.AddAction(uid, ref cultist.CommunionAction, CommunionAction);
        if (HasComp<HumanoidProfileComponent>(uid) && HasComp<HandsComponent>(uid))
            _actions.AddAction(uid, ref cultist.BloodMagicAction, PrepareMagicAction);
        _alerts.ShowAlert(uid, BloodSenseAlert, 0);

        if (_mind.TryGetMind(uid, out var mindId, out var mind))
        {
            if (!_role.MindHasRole<CultistRoleComponent>(mindId))
                _role.MindAddRole(mindId, MindRole, mind, silent: true);
            _rule.AddMember(rule, mindId, cultGhost);
            _rule.EnsureObjectives(rule, mindId, mind);
        }

        if (rule.Comp.Risen)
            SetRedEyes(uid, true);
        if (rule.Comp.Ascendent)
            SetHalo(uid, true);

        Dirty(uid, cultist);

        if (silent)
            return;

        // stinger_sound.
        SoundTo(uid, CultSounds.BloodcultGain);
        Message(uid, Loc.GetString("cult-greet"));
        if (equip)
            _rule.EquipCultist(uid, rule);
    }

    /// <summary>datum/antagonist/cult/on_removal().</summary>
    public void RemoveCultist(EntityUid uid)
    {
        if (!TryComp<CultistComponent>(uid, out var cultist))
            return;

        if (!cultist.Silent)
        {
            _popup.PopupEntity(Loc.GetString("cult-deconvert-others", ("target", uid)), uid, Filter.PvsExcept(uid), true, PopupType.Medium);
            Message(uid, Loc.GetString("cult-deconvert-self"));
        }

        if (cultist.Leader)
            DemoteLeader(uid, cultist);

        RemComp<CultistComponent>(uid);
    }

    private void OnCultistShutdown(Entity<CultistComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _faction.RemoveFaction(ent.Owner, CultFaction);
        _actions.RemoveAction(ent.Owner, ent.Comp.CommunionAction);
        _actions.RemoveAction(ent.Owner, ent.Comp.BloodMagicAction);
        foreach (var action in ent.Comp.MasterActions)
            _actions.RemoveAction(ent.Owner, action);
        _magic.ClearAllSpells(ent);
        _alerts.ClearAlert(ent.Owner, BloodSenseAlert);
        SetRedEyes(ent, false);
        SetHalo(ent, false);

        if (_mind.TryGetMind(ent, out var mindId, out var mind))
        {
            _role.MindRemoveRole<CultistRoleComponent>(mindId);
            if (_rule.TryGetRule(out var rule))
                _rule.RemoveMember(rule.Value, mindId);
        }
    }

    /// <summary>Совместимость со святой водой капеллана: сбросить подготовленные заклинания.</summary>
    public bool ClearPreparedSpells(EntityUid cultist)
    {
        if (!TryComp<CultistComponent>(cultist, out var comp) || comp.Spells.Count == 0)
            return false;
        _magic.ClearAllSpells((cultist, comp));
        return true;
    }

    /// <summary>Совместимость с капелланом: раскрыть скрытые руны и постройки.</summary>
    public int RevealConcealed(MapCoordinates center, float range)
    {
        return _structures.RevealAround(center, range);
    }

    #endregion

    #region Глаза и нимбы

    /// <summary>datum/element/cult_eyes.</summary>
    public void SetRedEyes(EntityUid uid, bool enabled)
    {
        if (!TryComp<CultistComponent>(uid, out var cultist) && enabled)
            return;
        if (cultist != null && cultist.RedEyes == enabled)
            return;
        if (cultist != null)
        {
            cultist.RedEyes = enabled;
            Dirty(uid, cultist);
        }

        if (!_visualBody.TryGatherMarkingsData(uid, null, out var profiles, out _, out _))
            return;

        if (enabled)
        {
            _originalEyes[uid] = profiles.ToDictionary(p => p.Key, p => p.Value);
            var red = profiles.ToDictionary(p => p.Key, p => p.Value with { EyeColor = Color.Red });
            _visualBody.ApplyProfiles(uid, red);
        }
        else if (_originalEyes.Remove(uid, out var original))
        {
            var restored = profiles.ToDictionary(p => p.Key,
                p => original.TryGetValue(p.Key, out var o) ? p.Value with { EyeColor = o.EyeColor } : p.Value);
            _visualBody.ApplyProfiles(uid, restored);
        }
    }

    /// <summary>status_effect/cult_halo.</summary>
    public void SetHalo(EntityUid uid, bool enabled)
    {
        if (!TryComp<CultistComponent>(uid, out var cultist))
            return;
        if (cultist.Halo == enabled)
            return;
        cultist.Halo = enabled;
        cultist.HaloState = _random.Next(1, 7);
        if (enabled)
        {
            EnsureComp<CultHaloVisualsComponent>(uid);
            Effect("CultEffectSparks", Transform(uid).Coordinates, randomDir: true);
        }
        else
        {
            RemComp<CultHaloVisualsComponent>(uid);
        }
        Dirty(uid, cultist);
    }

    /// <summary>datum/team/cult/check_size().</summary>
    public void CheckSize()
    {
        if (!_rule.TryGetRule(out var rule) || rule.Value.Comp.Ascendent)
            return;

        var alive = 0;
        var cultists = 0;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } mob || !HasComp<MobStateComponent>(mob) || _mobState.IsDead(mob))
                continue;
            if (IsCultist(mob))
                cultists++;
            else
                alive++;
        }

        if (cultists == 0)
            return;

        var comp = rule.Value.Comp;
        var highpop = alive >= comp.HighpopThreshold;
        var risenThreshold = highpop ? comp.RisenHighpop : comp.RisenLowpop;
        var ascendThreshold = highpop ? comp.AscendentHighpop : comp.AscendentLowpop;
        var ratio = alive > 0 ? (float) cultists / alive : 1f;

        if (ratio >= risenThreshold && !comp.Risen)
        {
            comp.Risen = true;
            foreach (var member in CultistBodies())
            {
                SoundTo(member, CultSounds.ISeeYou);
                Message(member, Loc.GetString("cult-risen"));
                // cult_eyes: initial_delay 20 секунд.
                var eyes = member;
                Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(20), () =>
                {
                    if (!TerminatingOrDeleted(eyes))
                        SetRedEyes(eyes, true);
                });
            }
        }

        if (ratio >= ascendThreshold && !comp.Ascendent)
        {
            comp.Ascendent = true;
            foreach (var member in CultistBodies())
            {
                SoundTo(member, CultSounds.ImHere);
                Message(member, Loc.GetString("cult-ascendent"));
                // Нимб появляется через 20 секунд (initial_delay).
                var target = member;
                Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(20), () =>
                {
                    if (!TerminatingOrDeleted(target))
                        SetHalo(target, true);
                });
            }

            _chat.DispatchGlobalAnnouncement(
                Loc.GetString("cult-ascendent-announcement", ("percent", (int) MathF.Floor(ratio * 100))),
                Loc.GetString("cult-paranormal-sender"),
                playSound: true);
        }
    }

    public IEnumerable<EntityUid> CultistBodies()
    {
        var query = EntityQueryEnumerator<CultistComponent>();
        while (query.MoveNext(out var uid, out _))
            yield return uid;
    }

    #endregion

    #region Общение

    private void OnLegacyCommune(Entity<CultistComponent> ent, ref CultCommuneActionEvent args)
    {
        args.Handled = true;
        OpenCommunion(ent);
    }

    private void OnCommunion(Entity<CultistComponent> ent, ref CultCommunionActionEvent args)
    {
        args.Handled = true;
        OpenCommunion(ent);
    }

    /// <summary>datum/action/innate/cult/comm.</summary>
    public void OpenCommunion(EntityUid user)
    {
        OpenText(user, Loc.GetString("cult-communion-title"), Loc.GetString("cult-communion-prompt"), 512, message =>
        {
            if (!IsCultAligned(user) || _mobState.IsIncapacitated(user))
                return;
            CultistCommune(user, message);
        });
    }

    /// <summary>cultist_commune().</summary>
    public void CultistCommune(EntityUid user, string message)
    {
        Whisper(user, "O bidai nabora se" + _random.Pick(new[] { "'", "`" }) + "sma!");
        Whisper(user, message);

        var leader = TryComp<CultistComponent>(user, out var cultist) && cultist.Leader;
        var title = leader
            ? Loc.GetString("cult-title-master")
            : HasComp<HumanoidProfileComponent>(user) ? Loc.GetString("cult-title-acolyte") : Loc.GetString("cult-title-construct");
        var text = Loc.GetString(leader ? "cult-communion-message-master" : "cult-communion-message",
            ("title", title), ("name", Name(user)), ("message", Robust.Shared.Utility.FormattedMessage.EscapeText(message)));
        MessageCult(text, ghosts: true);
    }

    #endregion

    #region Чувство крови

    /// <summary>
    /// alert/bloodsense: 0 cult_sense, 1 runed_sense0 (нужна жертва), 2 runed_sense1 (призыв), 3 runed_sense2 (цель рядом/не видна),
    /// 4+ — стрелка: (дальность * 16 + направление 0..15 по 22.5° по часовой от севера).
    /// </summary>
    private void UpdateBloodSense()
    {
        var rule = GetRule();

        var query = EntityQueryEnumerator<AlertsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            var construct = CompOrNull<CultConstructComponent>(uid);
            if (!HasComp<CultistComponent>(uid) && construct == null)
                continue;

            EntityUid? target = null;
            if (construct is { Seeking: true } && construct.SeekTarget is { } seek && !TerminatingOrDeleted(seek))
                target = seek;
            if (target == null && rule?.Comp.BloodTarget is { } blood && !TerminatingOrDeleted(blood))
                target = blood;

            short severity;
            if (target == null)
            {
                if (rule == null)
                    severity = 0;
                else if (!rule.Value.Comp.SacrificeDone && rule.Value.Comp.SacrificeTarget != null)
                    severity = 1;
                else
                    severity = 2;
            }
            else
            {
                var txform = Transform(target.Value);
                if (txform.MapID != xform.MapID)
                {
                    severity = 3;
                }
                else
                {
                    var delta = _transform.GetWorldPosition(txform) - _transform.GetWorldPosition(xform);
                    var dist = (int) MathF.Round(MathF.Max(MathF.Abs(delta.X), MathF.Abs(delta.Y)));
                    if (dist <= 1)
                    {
                        severity = 3;
                    }
                    else
                    {
                        // arrow8 (2-8) ... arrow0 (58-64), arrow (65+).
                        var bucket = dist >= 65 ? 9 : Math.Clamp((dist - 2) / 7, 0, 8);
                        var degrees = Math.Atan2(delta.X, delta.Y) * 180.0 / Math.PI;
                        if (degrees < 0)
                            degrees += 360;
                        var dir = (int) Math.Round(degrees / 22.5) % 16;
                        severity = (short) (4 + bucket * 16 + dir);
                    }
                }
            }

            _alerts.ShowAlert(uid, BloodSenseAlert, severity);
        }
    }

    #endregion

    private void OnCultistMobState(Entity<CultistComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || !ent.Comp.Leader)
            return;
        Deathrattle(ent);
    }
}
