using System.Linq;
using System.Numerics;
using Content.Shared.PDA;
using CancellationTokenSource = System.Threading.CancellationTokenSource;
using Content.Shared.Tag;
using Content.Server.Actions;
using Content.Server.Doors.Systems;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Antag;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Follower.Components;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Imperial.Heretic.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Overlays;
using Content.Shared.Popups;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;
using Content.Server.GameTicking.Rules;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Content.Server.Body.Components;
using Content.Shared.Temperature.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Standing;
using Content.Server.Decals;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Decals;
using Content.Shared.Chat;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared.Objectives.Systems;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticSystem : SharedHereticSystem
{
    private static readonly ProtoId<TagPrototype> HereticBladeTag = "HereticBlade";

    [Dependency] private readonly ActionsSystem         _actions  = default!;
    [Dependency] private readonly IPrototypeManager     _proto    = default!;
    [Dependency] private readonly IRobustRandom         _random   = default!;
    [Dependency] private readonly MindSystem            _mind     = default!;
    [Dependency] private readonly MobStateSystem        _mobs     = default!;
    [Dependency] private readonly PopupSystem           _popup    = default!;
    [Dependency] private readonly RoleSystem            _role     = default!;
    [Dependency] private readonly SharedHandsSystem     _hands    = default!;
    [Dependency] private readonly SharedStunSystem      _stun     = default!;
    [Dependency] private readonly SharedTransformSystem _xform    = default!;
    [Dependency] private readonly UserInterfaceSystem   _ui           = default!;
    [Dependency] private readonly DamageableSystem        _damageSystem      = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream       = default!;
    [Dependency] private readonly StatusEffectsSystem     _statusEffects     = default!;
    [Dependency] private readonly HereticStatusEffectsSystem _hereticEffects = default!;
    [Dependency] private readonly SharedAudioSystem          _audio          = default!;
    [Dependency] private readonly BlindableSystem            _blindable      = default!;
    [Dependency] private readonly FlammableSystem            _flammable      = default!;
    [Dependency] private readonly IdentitySystem             _identity       = default!;
    [Dependency] private readonly HereticPathActionsSystem   _pathActions    = default!;
    [Dependency] private readonly DoorSystem                 _door           = default!;
    [Dependency] private readonly SharedMechSystem           _mech           = default!;
    [Dependency] private readonly AppearanceSystem            _appearance     = default!;
    [Dependency] private readonly StandingStateSystem         _standing       = default!;
    [Dependency] private readonly EntityLookupSystem          _lookup         = default!;
    [Dependency] private readonly DecalSystem                 _decal          = default!;
    [Dependency] private readonly SharedMapSystem              _mapSystem      = default!;
    [Dependency] private readonly SharedChatSystem             _chat           = default!;
    [Dependency] private readonly ChatSystem                   _chatServer     = default!;
    [Dependency] private readonly SharedObjectivesSystem        _objectives     = default!;
    [Dependency] private readonly SharedJobSystem              _jobs           = default!;
    [Dependency] private readonly TagSystem                    _tag            = default!;
    [Dependency] private readonly InventorySystem              _inventory      = default!;
    [Dependency] private readonly SharedAccessSystem           _access         = default!;
    [Dependency] private readonly SharedStealthSystem          _stealth        = default!;
    [Dependency] private readonly HereticMoonBrainDamageSystem _moonBrainDamage = default!;
    [Dependency] private readonly IChatManager                 _chatManager     = default!;
    [Dependency] private readonly HereticAshPassiveSystem      _ashPassive      = default!;
    [Dependency] private readonly HereticMoonPassiveSystem     _moonPassive     = default!;
    [Dependency] private readonly HereticLockPassiveSystem     _lockPassive     = default!;
    [Dependency] private readonly HereticFleshPassiveSystem    _fleshPassive    = default!;
    [Dependency] private readonly HereticBladePassiveSystem    _bladePassive    = default!;
    [Dependency] private readonly HereticVoidPassiveSystem     _voidPassive     = default!;
    [Dependency] private readonly HereticRustPassiveSystem     _rustPassive     = default!;
    [Dependency] private readonly HereticCosmosPassiveSystem   _cosmosPassive   = default!;
    [Dependency] private readonly IGameTiming                  _timing          = default!;

    private const string RustDecalId = "Rust";

    private static readonly string[] DrainMessages =
    [
        "МЕРЦАНИЕ... ПОТЕНЦИАЛ... СИЛА.",
        "ШЁПОТ.",
        "ПОКРЫТ И ЗАБЫТ.",
        "ПРОКЛЯТАЯ ЗЕМЛЯ, ПРОКЛЯТЫЙ ЧЕЛОВЕК, ПРОКЛЯТЫЙ РАЗУМ.",
        "ВЕЛИКИЕ ВЫСОТЫ.",
        "ЗА МНОЙ НАБЛЮДАЮТ... ОТКУДА? ЧТО ЭТО?",
        "Я ОПАЗДЫВАЮ К СВОЕЙ СУДЬБЕ.",
        "ЖИЗНЬ МИМОЛЁТНА, НО ЧТО ОСТАЁТСЯ?",
        "ДОЖДЬ ИЗ КРОВИ. ЦАРСТВО КРОВИ.",
        "СИЛА... НЕСРАВНЕННАЯ. НЕНАТУРАЛЬНАЯ.",
        "ВРАТА МАНСУСА ЗДЕСЬ, ОТКРЫТЫ.",
        "ЧЕМ ВЫШЕ Я ПОДНИМАЮСЬ, ТЕМ БОЛЬШЕ ВИЖУ.",
        "ЗАВЕСА РАЗОРВАНА.",
        "ИХ РУКА РЯДОМ СО МНОЙ.",
        "ОНИ ХОДЯТ ПО МИРУ. НЕЗАМЕЧЕННЫЕ.",
        "ПОХОД СКВОЗЬ ИЗМЕРЕНИЯ."
    ];

    private static readonly string[] RustAscensionRuneEffects =
    [
        "HereticSmallRuneEffect1",  "HereticSmallRuneEffect2",  "HereticSmallRuneEffect3",
        "HereticSmallRuneEffect4",  "HereticSmallRuneEffect5",  "HereticSmallRuneEffect6",
        "HereticSmallRuneEffect7",  "HereticSmallRuneEffect8",  "HereticSmallRuneEffect9",
        "HereticSmallRuneEffect10", "HereticSmallRuneEffect11", "HereticSmallRuneEffect12"
    ];

    // Cancellation for GraspOfLunacy identity-hide timer (one per heretic)
    private readonly Dictionary<EntityUid, CancellationTokenSource> _graspLunacyCancels = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticComponent, HereticKnowledgeMenuActionEvent>(OnKnowledgeMenu);
        SubscribeLocalEvent<HereticComponent, HereticMansusGraspActionEvent>(OnMansusGrasp);
        SubscribeLocalEvent<HereticMansusGraspItemComponent, AfterInteractEvent>(OnMansusGraspItemAfterInteract);
        SubscribeLocalEvent<HereticMansusGraspItemComponent, SuicideByEnvironmentEvent>(OnMansusGraspItemSuicide);
        SubscribeLocalEvent<HereticKnowledgeHolderComponent, HereticResearchKnowledgeMessage>(OnResearchMessage);
        SubscribeLocalEvent<HereticKnowledgeHolderComponent, HereticSelectPathMessage>(OnSelectPathMessage);
        SubscribeLocalEvent<HereticKnowledgeHolderComponent, HereticDenyAscensionMessage>(OnDenyAscensionMessage);
        SubscribeLocalEvent<HereticKnowledgeHolderComponent, HereticTargetSelectedMessage>(OnTargetSelectedMessage);
        SubscribeLocalEvent<HereticKnowledgeHolderComponent, BoundUserInterfaceCheckRangeEvent>(OnBuiRangeCheck);
        SubscribeLocalEvent<HereticKnowledgeHolderComponent, BoundUIClosedEvent>(OnHolderBuiClosed);
        SubscribeLocalEvent<HereticMansusBookComponent, UseInHandEvent>(OnMansusBookUseInHand);
        SubscribeLocalEvent<HereticComponent, ComponentRemove>(OnHereticRemoved);
        SubscribeLocalEvent<MetaDataComponent, EntityTerminatingEvent>(OnEntityTerminating);
    }

    private void OnEntityTerminating(EntityUid uid, MetaDataComponent _, ref EntityTerminatingEvent args)
    {
        var query = EntityQueryEnumerator<HereticComponent>();
        while (query.MoveNext(out var hUid, out var comp))
        {
            if (hUid == uid)
                continue;
            if (comp.GrantedActions.Remove(uid) | comp.NamedTargets.Remove(uid) | comp.OrbitingBlades.Remove(uid))
                Dirty(hUid, comp);
        }
    }

    private void OnHereticRemoved(EntityUid uid, HereticComponent comp, ComponentRemove args)
    {
        _pathActions.CleanupEntity(uid);

        if (_graspLunacyCancels.Remove(uid, out var cts))
            cts.Cancel();

        ClearOrbitingBlades(uid, comp);

        foreach (var target in comp.NamedTargets)
        {
            if (!TryComp<HereticNamedTargetComponent>(target, out var targetComp))
                continue;
            targetComp.OwningHeretics.Remove(uid);
            if (targetComp.OwningHeretics.Count == 0)
                RemCompDeferred<HereticNamedTargetComponent>(target);
            else
                Dirty(target, targetComp);
        }
    }

    // ─── Public API ──────────────────────────────────────────────────────────

    public bool HasFocus(EntityUid uid, HereticComponent comp)
    {
        if (comp.AscensionTriggered)
            return true;

        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (HasComp<HereticCodexComponent>(held) ||
                HasComp<HereticAmberFocusComponent>(held) ||
                HasComp<HereticCodexMorbiusComponent>(held))
                return true;
        }

        foreach (var slot in new[] { "neck", "id", "pocket1", "pocket2", "head" })
        {
            if (_inventory.TryGetSlotEntity(uid, slot, out var slotEnt) &&
                (HasComp<HereticCodexComponent>(slotEnt.Value) ||
                 HasComp<HereticAmberFocusComponent>(slotEnt.Value) ||
                 HasComp<HereticCodexMorbiusComponent>(slotEnt.Value)))
                return true;
        }

        return false;
    }

    public bool HasMorbiusCodex(EntityUid uid)
    {
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (TryComp<HereticCodexComponent>(held, out var codex) && codex.CanCurseRunes)
                return true;
        }

        foreach (var slot in new[] { "neck", "id", "pocket1", "pocket2", "head" })
        {
            if (_inventory.TryGetSlotEntity(uid, slot, out var slotEnt) &&
                TryComp<HereticCodexComponent>(slotEnt.Value, out var slotCodex) && slotCodex.CanCurseRunes)
                return true;
        }

        return false;
    }

    public void AddKnowledgePoints(EntityUid uid, HereticComponent comp, int amount)
    {
        comp.KnowledgePoints += amount;
        comp.TotalKnowledgeGained += amount;
        SendInfoBuiState(uid, comp);
        Dirty(uid, comp);
    }

    public void SendHereticMessage(EntityUid uid, string message)
    {
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;
        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        _chatManager.ChatMessageToOne(ChatChannel.Server, message, wrapped, default, false, actor.PlayerSession.Channel);
    }

    public void ActivateHereticAura(EntityUid uid, HereticComponent comp)
    {
        var visComp = EnsureComp<HereticAuraVisualsComponent>(uid);
        if (visComp.HasEarnedAura)
            return;
        visComp.HasEarnedAura = true;
        if (!visComp.IsWearingRobe)
            ShowHereticAura(uid, visComp);

        comp.UnlimitedBlades = true;
        SendHereticMessage(uid, Loc.GetString("heretic-aura-unlocked-chat"));
        Dirty(uid, comp);
    }

    public void ShowHereticAura(EntityUid uid, HereticAuraVisualsComponent visComp)
    {
        if (visComp.AuraEntity != null && !Deleted(visComp.AuraEntity.Value))
            return;
        var effect = Spawn("HereticAuraEffect", Transform(uid).Coordinates);
        _xform.SetParent(effect, uid);
        visComp.AuraEntity = effect;
    }

    public void HideHereticAura(HereticAuraVisualsComponent visComp)
    {
        if (visComp.AuraEntity == null || Deleted(visComp.AuraEntity.Value))
            return;
        QueueDel(visComp.AuraEntity.Value);
        visComp.AuraEntity = null;
    }

    public void SpendKnowledgePoints(EntityUid uid, HereticComponent comp, int amount)
    {
        comp.KnowledgePoints -= amount;
        Dirty(uid, comp);
    }

    public void SetAscensionTriggered(EntityUid uid, HereticComponent comp)
    {
        comp.AscensionTriggered = true;
        Dirty(uid, comp);
    }

    public void SetFeastOfOwlsUsed(EntityUid uid, HereticComponent comp)
    {
        comp.FeastOfOwlsUsed = true;
        Dirty(uid, comp);
    }

    public void SetPassiveLevel(EntityUid uid, HereticComponent comp, int level)
    {
        if (comp.PassiveLevel >= level)
            return;

        comp.PassiveLevel = level;
        Dirty(uid, comp);
    }

    public void AddGrantedAction(EntityUid uid, HereticComponent comp, EntityUid actionEnt)
    {
        comp.GrantedActions.Add(actionEnt);
        Dirty(uid, comp);
    }

    public bool HasKnowledge(HereticComponent comp, ProtoId<HereticKnowledgePrototype> knowledgeId)
        => comp.ResearchedKnowledge.Contains(knowledgeId);

    public bool TryTickAuraAccumulator(EntityUid uid, HereticComponent comp, float frameTime, float interval)
    {
        comp.AuraAccumulator += frameTime;
        if (comp.AuraAccumulator < interval)
            return false;
        comp.AuraAccumulator = 0f;
        Dirty(uid, comp);
        return true;
    }

    public int GetSacrificeCount(EntityUid mindId)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.CurrentEntity == null)
            return 0;
        if (!TryComp<HereticComponent>(mind.CurrentEntity.Value, out var heretic))
            return 0;
        return heretic.SacrificeCount;
    }

    public int GetResearchedKnowledgeCount(EntityUid mindId)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.CurrentEntity == null)
            return 0;
        if (!TryComp<HereticComponent>(mind.CurrentEntity.Value, out var heretic))
            return 0;
        return heretic.TotalKnowledgeGained;
    }

    public bool IsAscended(EntityUid mindId)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.CurrentEntity == null)
            return false;
        if (!TryComp<HereticComponent>(mind.CurrentEntity.Value, out var heretic))
            return false;
        return heretic.AscensionTriggered;
    }

    public void SetAscensionBypass(EntityUid uid, bool value)
    {
        if (!TryComp<HereticComponent>(uid, out var comp)) return;
        comp.AscensionBypass = value;
        Dirty(uid, comp);
    }

    // ─── Sacrifice / Ascension helpers ───────────────────────────────────────

    public void AddSacrifice(EntityUid bodyUid, int amount = 1)
    {
        if (!TryComp<HereticComponent>(bodyUid, out var comp)) return;
        comp.SacrificeCount += amount;
        Dirty(bodyUid, comp);
    }

    public int GetHighValueSacrificeCount(EntityUid mindId)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.CurrentEntity == null)
            return 0;
        if (!TryComp<HereticComponent>(mind.CurrentEntity.Value, out var heretic))
            return 0;
        return heretic.HighValueSacrificeCount;
    }

    public void AddHighValueSacrifice(EntityUid bodyUid, int amount = 1)
    {
        if (!TryComp<HereticComponent>(bodyUid, out var comp)) return;
        comp.HighValueSacrificeCount += amount;
        Dirty(bodyUid, comp);
    }

    public int GetSummonCount(EntityUid mindId)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.CurrentEntity == null)
            return 0;
        if (!TryComp<HereticComponent>(mind.CurrentEntity.Value, out var heretic))
            return 0;
        return heretic.SummonCount;
    }

    public void AddSummon(EntityUid bodyUid, int amount = 1)
    {
        if (!TryComp<HereticComponent>(bodyUid, out var comp)) return;
        comp.SummonCount += amount;
        Dirty(bodyUid, comp);
    }

    public void AddAscensionCorpse(EntityUid bodyUid)
    {
        if (!TryComp<HereticComponent>(bodyUid, out var comp)) return;
        comp.AscensionCorpsesQualified++;
        Dirty(bodyUid, comp);
        TryTriggerAscension(bodyUid);
    }

    public void TryTriggerAscension(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp)) return;
        if (comp.AscensionTriggered) return;

        comp.AscensionTriggered = true;

        // Все пути: эффект возвышения срабатывает сразу, без action-кнопки
        _pathActions.ExecutePathAscension(uid, comp);

        _popup.PopupEntity(Loc.GetString("heretic-ascended"), uid, uid, PopupType.LargeCaution);

        var ascendSound = comp.CurrentPath switch
        {
            HereticPath.Ash    => "/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_ash.ogg",
            HereticPath.Lock   => "/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_knock.ogg",
            HereticPath.Flesh  => "/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_flesh.ogg",
            HereticPath.Void   => "/Audio/Imperial/heretic/ascend_void.ogg",
            HereticPath.Blade  => "/Audio/Imperial/heretic/sound_music_antag_heretic_ascend_blade.ogg",
            HereticPath.Rust   => "/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_rust.ogg",
            HereticPath.Moon   => "/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_moon.ogg",
            _                  => "/Audio/Imperial/heretic/sound_ambience_antag_heretic_heretic_gain.ogg",
        };
        _audio.PlayGlobal(new SoundPathSpecifier(ascendSound), Filter.Broadcast(), false);

        // Full heal on ascension
        _damageSystem.SetAllDamage(uid, 0);

        // Passive ascension bonus: 50% brute and burn damage resistance
        var buff = EnsureComp<DamageProtectionBuffComponent>(uid);
        buff.Modifiers["HereticAscension"] = new ProtoId<DamageModifierSetPrototype>("HereticAscended");
        Dirty(uid, buff);

        // Path-specific transformation popup
        var transformMsg = comp.CurrentPath switch
        {
            HereticPath.Ash    => "heretic-ascended-ash",
            HereticPath.Flesh  => "heretic-ascended-flesh",
            HereticPath.Void   => "heretic-ascended-void",
            HereticPath.Lock   => "heretic-ascended-lock",
            HereticPath.Blade  => "heretic-ascended-blade",
            HereticPath.Rust   => "heretic-ascended-rust",
            HereticPath.Moon   => "heretic-ascended-moon",
            _                  => null
        };
        if (transformMsg != null)
            _popup.PopupEntity(Loc.GetString(transformMsg), uid, uid, PopupType.LargeCaution);

        var ascensionChatMsg = comp.CurrentPath switch
        {
            HereticPath.Ash    => "heretic-ascension-message-ash",
            HereticPath.Flesh  => "heretic-ascension-message-flesh",
            HereticPath.Void   => "heretic-ascension-message-void",
            HereticPath.Lock   => "heretic-ascension-message-lock",
            HereticPath.Blade  => "heretic-ascension-message-blade",
            HereticPath.Rust   => "heretic-ascension-message-rust",
            HereticPath.Moon   => "heretic-ascension-message-moon",
            HereticPath.Cosmos => "heretic-ascension-message-cosmos",
            _                  => "heretic-ascension-message-general"
        };
        SendHereticMessage(uid, Loc.GetString(ascensionChatMsg));

        // Global music broadcast
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/heretic/heretic_music.ogg"), Filter.Broadcast(), true, AudioParams.Default.WithVolume(-10f));

        // CentComm announcement
        var hereticName = MetaData(uid).EntityName;
        var (ccKey, ccColor) = comp.CurrentPath switch
        {
            HereticPath.Ash    => ("heretic-ascension-cc-ash",    Color.FromHex("#FF8C42")),
            HereticPath.Flesh  => ("heretic-ascension-cc-flesh",  Color.FromHex("#E53935")),
            HereticPath.Void   => ("heretic-ascension-cc-void",   Color.FromHex("#8B6FFF")),
            HereticPath.Lock   => ("heretic-ascension-cc-lock",   Color.FromHex("#FFD700")),
            HereticPath.Blade  => ("heretic-ascension-cc-blade",  Color.FromHex("#B0BEC5")),
            HereticPath.Rust   => ("heretic-ascension-cc-rust",   Color.FromHex("#D2691E")),
            HereticPath.Moon   => ("heretic-ascension-cc-moon",   Color.FromHex("#90CAF9")),
            HereticPath.Cosmos => ("heretic-ascension-cc-cosmos", Color.FromHex("#00E5FF")),
            _                  => ("heretic-ascension-cc-announcement", Color.Gold)
        };
        _chatServer.DispatchGlobalAnnouncement(
            Loc.GetString(ccKey, ("name", hereticName)),
            playSound: false,
            colorOverride: ccColor);

        SetPassiveLevel(uid, comp, 3);

        Dirty(uid, comp);

        SendInfoBuiState(uid, comp);

        RaiseLocalEvent(new HereticAscendedEvent(uid));
    }

    public bool IsNamedTarget(EntityUid bodyUid, EntityUid targetBodyUid)
    {
        if (!TryComp<HereticComponent>(bodyUid, out var comp)) return false;
        return comp.NamedTargets.Contains(targetBodyUid);
    }

    public HereticPath GetCurrentPath(EntityUid bodyUid)
    {
        if (!TryComp<HereticComponent>(bodyUid, out var comp)) return HereticPath.General;
        return comp.CurrentPath;
    }

    public void AssignNamedTargets(EntityUid bodyUid, EntityUid hereticMindId)
    {
        if (!TryComp<HereticComponent>(bodyUid, out var comp)) return;

        // Determine the heretic's own primary department so we can pick one colleague as a target.
        string? hereticDeptId = null;
        if (_jobs.MindTryGetJob(hereticMindId, out var hereticJob) &&
            _jobs.TryGetPrimaryDepartment(hereticJob.ID, out var hereticDept))
        {
            hereticDeptId = hereticDept.ID;
        }

        // Bucket every other mind into job-based categories.
        var commandPool  = new List<EntityUid>();
        var securityPool = new List<EntityUid>();
        var sameDeptPool = new List<EntityUid>();
        var otherPool    = new List<EntityUid>();

        var mindQuery = EntityQueryEnumerator<MindComponent>();
        while (mindQuery.MoveNext(out var mindEnt, out var mindComp))
        {
            if (mindEnt == hereticMindId) continue;
            if (mindComp.CurrentEntity == null) continue;
            var bodyEnt = mindComp.CurrentEntity.Value;

            string? deptId = null;
            if (_jobs.MindTryGetJob(mindEnt, out var candidateJob) &&
                _jobs.TryGetPrimaryDepartment(candidateJob.ID, out var candidateDept))
            {
                deptId = candidateDept.ID;
            }

            if (deptId == "Command")
                commandPool.Add(bodyEnt);
            else if (deptId == "Security")
                securityPool.Add(bodyEnt);
            else if (hereticDeptId != null && deptId == hereticDeptId)
                sameDeptPool.Add(bodyEnt);
            else
                otherPool.Add(bodyEnt);
        }

        _random.Shuffle(commandPool);
        _random.Shuffle(securityPool);
        _random.Shuffle(sameDeptPool);
        _random.Shuffle(otherPool);

        var selected = new List<EntityUid>();

        // 1 command staff
        if (commandPool.Count > 0)
            selected.Add(commandPool[0]);

        // 1 security officer
        if (securityPool.Count > 0)
            selected.Add(securityPool[0]);

        // 1 crewmate from the heretic's own department
        if (sameDeptPool.Count > 0)
            selected.Add(sameDeptPool[0]);

        // Fill remaining slots (up to 5 total) from everyone else.
        var fallback = commandPool.Skip(selected.Contains(commandPool.Count > 0 ? commandPool[0] : EntityUid.Invalid) ? 1 : 0)
            .Concat(securityPool.Skip(selected.Contains(securityPool.Count > 0 ? securityPool[0] : EntityUid.Invalid) ? 1 : 0))
            .Concat(sameDeptPool.Skip(selected.Contains(sameDeptPool.Count > 0 ? sameDeptPool[0] : EntityUid.Invalid) ? 1 : 0))
            .Concat(otherPool)
            .Where(e => !selected.Contains(e))
            .ToList();
        _random.Shuffle(fallback);

        while (selected.Count < 5 && fallback.Count > 0)
        {
            selected.Add(fallback[0]);
            fallback.RemoveAt(0);
        }

        // Remove HUD marker from old targets before reassigning.
        foreach (var oldTarget in comp.NamedTargets)
        {
            if (!TryComp<HereticNamedTargetComponent>(oldTarget, out var oldMarker))
                continue;
            oldMarker.OwningHeretics.Remove(bodyUid);
            if (oldMarker.OwningHeretics.Count == 0)
                RemCompDeferred<HereticNamedTargetComponent>(oldTarget);
            else
                Dirty(oldTarget, oldMarker);
        }

        comp.NamedTargets = selected;

        // Add HUD marker to new targets.
        foreach (var target in comp.NamedTargets)
        {
            var marker = EnsureComp<HereticNamedTargetComponent>(target);
            if (!marker.OwningHeretics.Contains(bodyUid))
            {
                marker.OwningHeretics.Add(bodyUid);
                Dirty(target, marker);
            }
        }

        if (comp.NamedTargets.Count > 0)
        {
            var names = string.Join(", ", comp.NamedTargets.Select(t => MetaData(t).EntityName));
            _popup.PopupEntity(Loc.GetString("heretic-named-targets-assigned", ("names", names)), bodyUid, bodyUid, PopupType.LargeCaution);
            SendHereticMessage(bodyUid, Loc.GetString("heretic-named-targets-chat", ("names", names)));
        }

        Dirty(bodyUid, comp);
    }

    /// <summary>
    public bool IsNamedTarget(HereticComponent comp, EntityUid targetUid)
    {
        return comp.NamedTargets.Contains(targetUid);
    }

    /// <summary>
    /// Removes a single named target from the heretic's list, cleaning up HUD markers.
    /// </summary>
    public void RemoveNamedTarget(EntityUid bodyUid, HereticComponent comp, EntityUid targetUid)
    {
        if (!comp.NamedTargets.Remove(targetUid))
            return;

        if (TryComp<HereticNamedTargetComponent>(targetUid, out var marker))
        {
            marker.OwningHeretics.Remove(bodyUid);
            if (marker.OwningHeretics.Count == 0)
                RemCompDeferred<HereticNamedTargetComponent>(targetUid);
            else
                Dirty(targetUid, marker);
        }

        Dirty(bodyUid, comp);
    }

    /// <summary>
    /// Replaces the heretic's named targets with the given list, updating HUD markers.
    /// </summary>
    public void SetNamedTargets(EntityUid bodyUid, HereticComponent comp, List<EntityUid> targets)
    {
        foreach (var oldTarget in comp.NamedTargets)
        {
            if (!TryComp<HereticNamedTargetComponent>(oldTarget, out var oldMarker)) continue;
            oldMarker.OwningHeretics.Remove(bodyUid);
            if (oldMarker.OwningHeretics.Count == 0)
                RemCompDeferred<HereticNamedTargetComponent>(oldTarget);
            else
                Dirty(oldTarget, oldMarker);
        }

        comp.NamedTargets = targets;

        foreach (var target in comp.NamedTargets)
        {
            var marker = EnsureComp<HereticNamedTargetComponent>(target);
            if (!marker.OwningHeretics.Contains(bodyUid))
            {
                marker.OwningHeretics.Add(bodyUid);
                Dirty(target, marker);
            }
        }

        Dirty(bodyUid, comp);
    }

    /// <summary>
    /// Opens the HeartbeatMansus target-tracking BUI for the heretic.
    /// </summary>
    public void OpenTargetBui(EntityUid uid, HereticComponent comp)
    {
        if (comp.BuiHolder == EntityUid.Invalid || !Exists(comp.BuiHolder))
            return;
        SendTargetBuiState(uid, comp);
        _ui.TryOpenUi(comp.BuiHolder, HereticTargetBuiKey.Key, uid);
    }

    private void OnTargetSelectedMessage(EntityUid holderUid, HereticKnowledgeHolderComponent holderComp, HereticTargetSelectedMessage args)
    {
        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_singlebeat.ogg"), Filter.Broadcast(), false);
    }

    private void SendTargetBuiState(EntityUid uid, HereticComponent comp)
    {
        var targets = new List<HereticTargetData>();
        foreach (var targetUid in comp.NamedTargets)
        {
            if (!Exists(targetUid)) continue;
            var isDead = TryComp<MobStateComponent>(targetUid, out var mobState) &&
                         mobState.CurrentState == MobState.Dead;
            targets.Add(new HereticTargetData
            {
                Entity = GetNetEntity(targetUid),
                Name   = MetaData(targetUid).EntityName,
                IsDead = isDead,
            });
        }
        _ui.SetUiState(comp.BuiHolder, HereticTargetBuiKey.Key, new HereticTargetBuiState { Targets = targets });
    }

    public void MakeHeretic(EntityUid uid)
    {
        var comp = EnsureComp<HereticComponent>(uid);

        // Spawn knowledge BUI holder as child
        var holder = Spawn("HereticKnowledgeHolder", Transform(uid).Coordinates);
        _xform.SetParent(holder, uid);
        comp.BuiHolder = holder;

        EnsureComp<ShowAntagIconsComponent>(uid);
        EnsureComp<ShowHealthBarsComponent>(uid);

        if (_mind.TryGetMind(uid, out var mindId, out var mind))
        {
            if (!_role.MindHasRole<HereticRoleComponent>(mindId))
                _role.MindAddRole(mindId, "MindRoleHeretic", mind: mind, silent: true);
        }

        // Grant starting abilities
        GrantKnowledge(uid, comp, "KnowledgeBreakOfDawn");
        GrantKnowledge(uid, comp, "KnowledgeMansusGrasp");
        GrantKnowledge(uid, comp, "KnowledgeKnowledgeMenu");
        GrantKnowledge(uid, comp, "KnowledgeCloakOfShadow");
        GrantKnowledge(uid, comp, "KnowledgeHeartbeatMansus");
        GrantKnowledge(uid, comp, "KnowledgeAmberFocus");
        GrantKnowledge(uid, comp, "KnowledgeLivingHeart");
        GrantKnowledge(uid, comp, "KnowledgeFeastOfOwls");

        _audio.PlayGlobal(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_ambience_antag_heretic_heretic_gain.ogg"), Filter.Entities(uid), false);
        SendHereticMessage(uid, Loc.GetString("heretic-start-round-message"));
        Dirty(uid, comp);
    }

    public void SetAscensionTargets(EntityUid uid, int requiredSacrifices, int requiredKnowledge)
    {
        if (!TryComp<HereticComponent>(uid, out var comp))
            return;
        comp.RequiredSacrifices = requiredSacrifices;
        comp.RequiredKnowledge = requiredKnowledge;
        Dirty(uid, comp);
    }

    // ─── Knowledge ───────────────────────────────────────────────────────────

    public bool TryResearchKnowledge(EntityUid uid, HereticComponent comp, string knowledgeId)
    {
        if (!_proto.TryIndex<HereticKnowledgePrototype>(knowledgeId, out var proto))
            return false;

        if (comp.ResearchedKnowledge.Contains(knowledgeId))
        {
            _popup.PopupEntity(Loc.GetString("heretic-knowledge-already-known"), uid, uid);
            return false;
        }

        // Conflicts check: block if any mutually-exclusive node is already researched
        foreach (var conflict in proto.ConflictsWith)
        {
            if (comp.ResearchedKnowledge.Contains(conflict.Id))
            {
                _popup.PopupEntity(Loc.GetString("heretic-knowledge-already-known"), uid, uid);
                return false;
            }
        }

        var isGift = comp.PendingGiftGroups.Any(g => g.Candidates.Contains(knowledgeId));
        var effectiveCost = isGift ? 0 : proto.Cost;

        if (!isGift && comp.CurrentPath == HereticPath.Lock && comp.PassiveLevel >= 1 && proto.Path == HereticPath.General)
            effectiveCost = Math.Max(0, effectiveCost - 1);

        if (comp.KnowledgePoints < effectiveCost)
        {
            _popup.PopupEntity(Loc.GetString("heretic-knowledge-no-points"), uid, uid);
            return false;
        }

        // Prerequisites check
        if (!ArePrerequisitesMet(comp, proto))
        {
            _popup.PopupEntity(Loc.GetString("heretic-knowledge-prereq-missing"), uid, uid);
            return false;
        }

        // Shop level check: General shop nodes require sufficient ShopLevel (gifts bypass this)
        if (!isGift && proto.ShopLevel > 0 && comp.ShopLevel < proto.ShopLevel)
        {
            _popup.PopupEntity(Loc.GetString("heretic-knowledge-prereq-missing"), uid, uid);
            return false;
        }

        // Path lock check: if heretic already has a path, only allow General or same path
        if (proto.SetsPath && comp.CurrentPath != HereticPath.General && proto.Path != comp.CurrentPath)
        {
            _popup.PopupEntity(Loc.GetString("heretic-knowledge-wrong-path"), uid, uid);
            return false;
        }

        // Ascension gate: all non-ascension objectives must be complete first
        if (knowledgeId.StartsWith("KnowledgeAscension") && !comp.AscensionBypass)
        {
            if (_mind.TryGetMind(uid, out var mindId, out var mind))
            {
                foreach (var obj in mind.Objectives)
                {
                    if (HasComp<HereticAscensionConditionComponent>(obj))
                        continue;
                    if (!_objectives.IsCompleted(obj, (mindId, mind)))
                    {
                        _popup.PopupEntity(Loc.GetString("heretic-ascension-goals-incomplete"), uid, uid, PopupType.MediumCaution);
                        return false;
                    }
                }
            }
        }

        comp.KnowledgePoints -= effectiveCost;
        comp.ResearchedKnowledge.Add(knowledgeId);
        comp.TotalShopCostLearned += proto.Cost;
        if (comp.TotalShopCostLearned >= 8)
            ActivateHereticAura(uid, comp);

        if (isGift)
            comp.PendingGiftGroups.RemoveAll(g => g.Candidates.Contains(knowledgeId));

        if (proto.SetsPath && comp.CurrentPath == HereticPath.General)
            comp.CurrentPath = proto.Path;

        GrantKnowledgeActions(uid, comp, proto);
        ApplyPassiveKnowledgeEffect(uid, comp, knowledgeId);
        // Blades are obtained exclusively via ritual crafting (RitualBlade*Craft/Upgrade) — no auto-spawn on knowledge research.

        // Advance shop level when a path-specific node is researched (blade upgrades are excluded)
        if (proto.Path != HereticPath.General && !proto.SetsPath && proto.AdvancesShopLevel && comp.ShopLevel < 5)
        {
            comp.ShopLevel = Math.Min(comp.ShopLevel + 1, 5);
            var candidates = new List<string>();
            foreach (var shopProto in _proto.EnumeratePrototypes<HereticKnowledgePrototype>())
            {
                if (shopProto.ShopLevel == comp.ShopLevel && !comp.ResearchedKnowledge.Contains(shopProto.ID))
                    candidates.Add(shopProto.ID);
            }
            _random.Shuffle(candidates);
            var giftCandidates = new List<string>();
            for (var i = 0; i < Math.Min(3, candidates.Count); i++)
                giftCandidates.Add(candidates[i]);
            if (giftCandidates.Count > 0)
                comp.PendingGiftGroups.Add(new HereticGiftGroup { SourceNodeId = knowledgeId, Candidates = giftCandidates });
        }

        Dirty(uid, comp);
        SendInfoBuiState(uid, comp);

        _popup.PopupEntity(Loc.GetString("heretic-knowledge-learned", ("name", Loc.GetString(proto.Name))), uid, uid, PopupType.Medium);
        SendHereticMessage(uid, Loc.GetString("heretic-knowledge-gained-chat", ("name", Loc.GetString(proto.Name))));
        return true;
    }

    /// <summary>
    /// All base <see cref="HereticKnowledgePrototype.Prerequisites"/> must be researched, AND
    /// (if any <see cref="HereticKnowledgePrototype.PrerequisitesAny"/> sets are defined) at least
    /// one of those alternative sets must be fully researched.
    /// </summary>
    private static bool ArePrerequisitesMet(HereticComponent comp, HereticKnowledgePrototype proto)
    {
        if (!proto.Prerequisites.All(p => comp.ResearchedKnowledge.Contains(p)))
            return false;

        if (proto.PrerequisitesAny.Count == 0)
            return true;

        return proto.PrerequisitesAny.Any(set => set.All(p => comp.ResearchedKnowledge.Contains(p)));
    }

    private void GrantKnowledge(EntityUid uid, HereticComponent comp, string knowledgeId)
    {
        if (!_proto.TryIndex<HereticKnowledgePrototype>(knowledgeId, out var proto))
            return;

        if (comp.ResearchedKnowledge.Contains(knowledgeId))
            return;

        comp.ResearchedKnowledge.Add(knowledgeId);
        GrantKnowledgeActions(uid, comp, proto);
    }

    public void GrantMarkKnowledge(EntityUid uid, HereticComponent comp, string knowledgeId)
        => GrantKnowledge(uid, comp, knowledgeId);

    private void GrantKnowledgeActions(EntityUid uid, HereticComponent comp, HereticKnowledgePrototype proto)
    {
        foreach (var actionProtoId in proto.GrantActions)
        {
            EntityUid? actionEnt = null;
            _actions.AddAction(uid, ref actionEnt, actionProtoId);
            if (actionEnt.HasValue)
                comp.GrantedActions.Add(actionEnt.Value);
        }
    }

    // ─── BUI ─────────────────────────────────────────────────────────────────

    private void OnKnowledgeMenu(EntityUid uid, HereticComponent comp, HereticKnowledgeMenuActionEvent args)
    {
        args.Handled = true;
        if (comp.BuiHolder == EntityUid.Invalid || !Exists(comp.BuiHolder))
            return;

        OpenInfoBui(uid, comp);
    }

    private void OnMansusBookUseInHand(EntityUid uid, HereticMansusBookComponent comp, UseInHandEvent args)
    {
        if (!TryComp<HereticComponent>(args.User, out var hereticComp))
            return;

        args.Handled = true;
        hereticComp.MansusBook = uid;
        if (hereticComp.BuiHolder == EntityUid.Invalid || !Exists(hereticComp.BuiHolder))
            return;

        OpenInfoBui(args.User, hereticComp);

        comp.NextState = HereticMansusBookVisualState.Open;
        comp.TransitionTimeRemaining = comp.OpeningAnimDuration;
        _appearance.SetData(uid, HereticMansusBookVisuals.State, HereticMansusBookVisualState.Opening);
    }

    private void OnHolderBuiClosed(EntityUid holderUid, HereticKnowledgeHolderComponent holderComp, BoundUIClosedEvent args)
    {
        if (!TryComp<HereticComponent>(args.Actor, out var hereticComp))
            return;

        var book = hereticComp.MansusBook;
        if (book == EntityUid.Invalid || !Exists(book) || !TryComp<HereticMansusBookComponent>(book, out var bookComp))
            return;

        bookComp.NextState = HereticMansusBookVisualState.Closed;
        bookComp.TransitionTimeRemaining = bookComp.ClosingAnimDuration;
        _appearance.SetData(book, HereticMansusBookVisuals.State, HereticMansusBookVisualState.Closing);
    }

    private void OpenInfoBui(EntityUid uid, HereticComponent comp)
    {
        SendInfoBuiState(uid, comp);
        _ui.TryOpenUi(comp.BuiHolder, HereticInfoBuiKey.Key, uid);
    }

    private void OnSelectPathMessage(EntityUid holderUid, HereticKnowledgeHolderComponent holderComp, HereticSelectPathMessage args)
    {
        if (!TryComp<HereticComponent>(args.Actor, out var comp))
            return;
        if (comp.CurrentPath != HereticPath.General)
            return;

        TryResearchKnowledge(args.Actor, comp, args.KnowledgeId);
        comp.PassiveLevel = 1;

        if (comp.CurrentPath == HereticPath.Ash)
            _ashPassive.ApplyPassiveLevel1(args.Actor);

        if (comp.CurrentPath == HereticPath.Moon)
            _moonPassive.ApplyPassiveLevel1(args.Actor);

        if (comp.CurrentPath == HereticPath.Lock)
            _lockPassive.ApplyPassiveLevel1(args.Actor);

        if (comp.CurrentPath == HereticPath.Blade)
            _bladePassive.ApplyPassiveLevel1(args.Actor);

        if (comp.CurrentPath == HereticPath.Flesh)
            GrantKnowledge(args.Actor, comp, "KnowledgeMarkOfFlesh");

        if (comp.CurrentPath == HereticPath.Void)
            _voidPassive.ApplyPassiveLevel1(args.Actor);

        if (comp.CurrentPath == HereticPath.Rust)
            _rustPassive.ApplyPassiveLevel1(args.Actor);

        if (comp.CurrentPath == HereticPath.Cosmos)
            _cosmosPassive.ApplyPassiveLevel1(args.Actor);

        Dirty(args.Actor, comp);
        SendInfoBuiState(args.Actor, comp);
        _ui.TryOpenUi(holderUid, HereticInfoBuiKey.Key, args.Actor);
    }

    private void OnResearchMessage(EntityUid holderUid, HereticKnowledgeHolderComponent holderComp, HereticResearchKnowledgeMessage args)
    {
        if (!TryComp<HereticComponent>(args.Actor, out var comp))
            return;

        TryResearchKnowledge(args.Actor, comp, args.KnowledgeId);
    }

    private void OnBuiRangeCheck(EntityUid uid, HereticKnowledgeHolderComponent comp, BoundUserInterfaceCheckRangeEvent args)
    {
        args.Result = BoundUserInterfaceRangeResult.Pass;
    }

    private void OnDenyAscensionMessage(EntityUid uid, HereticKnowledgeHolderComponent comp, HereticDenyAscensionMessage args)
    {
        if (!TryComp<HereticComponent>(args.Actor, out var heretic))
            return;
        heretic.AscensionDenied = true;
        SendInfoBuiState(args.Actor, heretic);
    }

    public void SendInfoBuiState(EntityUid uid, HereticComponent comp)
    {
        if (comp.BuiHolder == EntityUid.Invalid || !Exists(comp.BuiHolder))
            return;

        var nodes = new List<HereticKnowledgeNodeData>();
        foreach (var proto in _proto.EnumeratePrototypes<HereticKnowledgePrototype>())
        {
            var prereqsMet = ArePrerequisitesMet(comp, proto);
            nodes.Add(new HereticKnowledgeNodeData
            {
                Id              = proto.ID,
                Name            = Loc.GetString(proto.Name),
                Description     = Loc.GetString(proto.Description),
                Cost            = proto.Cost,
                Path            = proto.Path,
                IsResearched    = comp.ResearchedKnowledge.Contains(proto.ID),
                PrerequisitesMet = prereqsMet,
                Icon            = proto.Icon,
                Prerequisites   = proto.Prerequisites.Select(p => p.Id).ToList(),
                PrerequisitesAny = proto.PrerequisitesAny.Select(set => set.Select(p => p.Id).ToList()).ToList(),
                IsGift          = proto.IsGift,
                ShopLevel       = proto.ShopLevel,
                ConflictsWith   = proto.ConflictsWith.Select(p => p.Id).ToList(),
            });
        }

        var paths = new List<HereticPathData>();
        foreach (var proto in _proto.EnumeratePrototypes<HereticPathPrototype>())
        {
            paths.Add(new HereticPathData
            {
                Id                 = proto.ID,
                Path               = proto.Path,
                Name               = Loc.GetString(proto.Name),
                Description        = Loc.GetString(proto.Description),
                Complexity         = Loc.GetString(proto.Complexity),
                PassiveName        = Loc.GetString(proto.PassiveName),
                PassiveDescription = Loc.GetString(proto.PassiveDescription),
                Pros               = Loc.GetString(proto.Pros),
                Cons               = string.IsNullOrEmpty(proto.Cons) ? string.Empty : Loc.GetString(proto.Cons),
                Level1Description  = string.IsNullOrEmpty(proto.Level1Description) ? string.Empty : Loc.GetString(proto.Level1Description),
                Level2Description  = string.IsNullOrEmpty(proto.Level2Description) ? string.Empty : Loc.GetString(proto.Level2Description),
                Level3Description  = string.IsNullOrEmpty(proto.Level3Description) ? string.Empty : Loc.GetString(proto.Level3Description),
                Icon               = proto.Icon,
                PathKnowledgeId    = proto.PathKnowledgeId,
                Tips               = string.IsNullOrEmpty(proto.Tips) ? string.Empty : Loc.GetString(proto.Tips),
            });
        }
        paths.Sort((a, b) => (int) a.Path - (int) b.Path);

        _ui.SetUiState(comp.BuiHolder, HereticInfoBuiKey.Key, new HereticInfoBuiState
        {
            KnowledgePoints    = comp.KnowledgePoints,
            CurrentPath        = comp.CurrentPath,
            PassiveLevel       = comp.PassiveLevel,
            SacrificeCount     = comp.SacrificeCount,
            RequiredSacrifices = comp.RequiredSacrifices,
            RequiredKnowledge  = comp.RequiredKnowledge,
            Paths              = paths,
            Nodes              = nodes,
            CurrentShopLevel   = comp.ShopLevel,
            PendingGiftGroups  = comp.PendingGiftGroups.Select(g => new HereticGiftGroup { SourceNodeId = g.SourceNodeId, Candidates = new List<string>(g.Candidates) }).ToList(),
        });
    }

    // ─── Mansus Grasp ────────────────────────────────────────────────────────

    private void OnMansusGrasp(EntityUid uid, HereticComponent comp, HereticMansusGraspActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;

        // SS13: on_grasp_cast — если в активной руке тёмный клинок и есть знание, вливаем силу вместо обычной Хватки
        if (comp.CurrentPath == HereticPath.Blade && comp.ResearchedKnowledge.Contains("KnowledgeEmpoweredBlades"))
        {
            var activeItem = _hands.GetActiveItem(uid);
            if (activeItem != null && TryComp<HereticBladeBladeComponent>(activeItem.Value, out var bladeComp) && !bladeComp.Infused)
            {
                bladeComp.Infused = true;
                Dirty(activeItem.Value, bladeComp);
                _appearance.SetData(activeItem.Value, HereticBladeSunderedVisuals.Infused, true);
                foreach (var held in _hands.EnumerateHeld(uid))
                {
                    if (held == activeItem.Value) continue;
                    if (!TryComp<HereticBladeBladeComponent>(held, out var offBladeComp) || offBladeComp.Infused) continue;
                    offBladeComp.Infused = true;
                    Dirty(held, offBladeComp);
                    _appearance.SetData(held, HereticBladeSunderedVisuals.Infused, true);
                }
                _popup.PopupEntity(Loc.GetString("heretic-empowered-blades-infused"), uid, uid, PopupType.Medium);
                return; // SS13: COMPONENT_CAST_HANDLESS — поглощает каст Хватки
            }
        }

        // Если уже держит предмет Хватки Мансуса — отменить
        if (TryFindAndDeleteMansusGraspItem(uid))
        {
            _popup.PopupEntity(Loc.GetString("heretic-grasp-cancelled"), uid, uid);
            return;
        }

        var item = Spawn("HereticMansusGraspItem", Transform(uid).Coordinates);
        if (!_hands.TryPickupAnyHand(uid, item))
        {
            QueueDel(item);
            _popup.PopupEntity(Loc.GetString("heretic-grasp-hands-full"), uid, uid);
        }
    }

    private bool TryFindAndDeleteMansusGraspItem(EntityUid uid)
    {
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (!HasComp<HereticMansusGraspItemComponent>(held))
                continue;

            RemComp<UnremoveableComponent>(held);
            QueueDel(held);
            return true;
        }
        return false;
    }

    private void OnMansusGraspItemSuicide(EntityUid uid, HereticMansusGraspItemComponent itemComp, SuicideByEnvironmentEvent args)
    {
        if (args.Handled || TerminatingOrDeleted(uid))
            return;

        var victim = args.Victim;
        if (!HasComp<HereticComponent>(victim) || !HasComp<DamageableComponent>(victim))
            return;

        args.Handled = true;

        _popup.PopupEntity(Loc.GetString("heretic-grasp-suicide"), victim, PopupType.LargeCaution);
        _chat.TryEmoteWithChat(victim, "Scream", ChatTransmitRange.Normal, ignoreActionBlocker: true);

        DoMansusGraspSuicideTick(victim, 0);
    }

    private void DoMansusGraspSuicideTick(EntityUid victim, int tick)
    {
        if (tick > 20 || TerminatingOrDeleted(victim) || !_mobs.IsAlive(victim))
            return;

        if (_random.Prob(0.7f))
        {
            var dmg = new DamageSpecifier();
            dmg.DamageDict["Heat"] = FixedPoint2.New(20);
            _damageSystem.TryChangeDamage(victim, dmg, ignoreResistances: true);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/sizzle.ogg"), victim);

            if (_random.Prob(0.5f))
            {
                _chat.TryEmoteWithChat(victim, "Scream", ChatTransmitRange.Normal, ignoreActionBlocker: true);
                _statusEffects.TryAddStatusEffect(victim, "Stutter", TimeSpan.FromSeconds(26), true, "StutteringAccentComponent");
            }
        }

        if (!_mobs.IsAlive(victim))
            return;

        Timer.Spawn(TimeSpan.FromSeconds(0.4), () => DoMansusGraspSuicideTick(victim, tick + 1));
    }

    private void OnMansusGraspItemAfterInteract(EntityUid uid, HereticMansusGraspItemComponent itemComp, AfterInteractEvent args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        var caster = args.User;
        if (!TryComp<HereticComponent>(caster, out var comp))
            return;

        if (args.Target is not {} target)
        {
            if (args.CanReach && !args.Handled)
            {
                args.Handled = true;
                RaiseLocalEvent(caster, new HereticStartSlowRuneDrawEvent());
            }
            return;
        }

        if (caster == target)
            return;

        // SS220: Lionhunter's Rifle — Хватка Мансуса по дальней цели мгновенно переносит к ней
        var canReach = args.CanReach;
        if (!canReach)
        {
            if (!comp.ResearchedKnowledge.Contains("KnowledgeLionhunterRifle"))
                return;
            if (!_mobs.IsAlive(target))
                return;

            _xform.SetCoordinates(caster, Transform(target).Coordinates);
            Spawn("HereticEffectMansusMarkBlade", Transform(caster).Coordinates);
        }

        args.Handled = true;

        // Mansus Grasp wipes heretic runes on touch, just like the verb-based erase but instant.
        if (HasComp<HereticRuneComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("heretic-grasp-rune-erased"), caster, caster, PopupType.Medium);
            Spawn("HereticEffectRuneFail", Transform(target).Coordinates);
            QueueDel(target);
            RemComp<UnremoveableComponent>(uid);
            QueueDel(uid);
            return;
        }

        if (comp.CurrentPath == HereticPath.Lock
            && comp.ResearchedKnowledge.Contains("KnowledgeLockwielder"))
        {
            // SS13: ismecha → eject + Paralyze(5s)
            if (TryComp<MechComponent>(target, out var mechComp))
            {
                var pilot = mechComp.PilotSlot.ContainedEntity;
                _mech.TryEject(target, mechComp);
                if (pilot.HasValue)
                    _stun.TryAddParalyzeDuration(pilot.Value, TimeSpan.FromSeconds(5));
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), caster);
                _popup.PopupEntity(Loc.GetString("heretic-grasp-lock-mech"), caster, caster, PopupType.Small);
                RemComp<UnremoveableComponent>(uid);
                QueueDel(uid);
                return;
            }

            // SS13: istype(airlock) → door.unbolt() then open
            if (HasComp<DoorComponent>(target))
            {
                if (TryComp<DoorBoltComponent>(target, out var boltComp))
                    _door.SetBoltsDown((target, boltComp), false);
                _door.TryOpen(target);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), caster);
                _popup.PopupEntity(Loc.GetString("heretic-grasp-lock-door"), caster, caster, PopupType.Small);
                if (comp.PassiveLevel >= 3)
                    _lockPassive.TryResetGraspCooldown(caster);
                RemComp<UnremoveableComponent>(uid);
                QueueDel(uid);
                return;
            }

            // SS13: istype(computer) → computer.authenticated = TRUE (GotEmaggedEvent.Access)
            var emagEvent = new GotEmaggedEvent(caster, EmagType.Access);
            RaiseLocalEvent(target, ref emagEvent);
            if (emagEvent.Handled)
            {
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), caster);
                _popup.PopupEntity(Loc.GetString("heretic-grasp-lock-console"), caster, caster, PopupType.Small);
                if (comp.PassiveLevel >= 3 && HasComp<EntityStorageComponent>(target))
                    _lockPassive.TryResetGraspCooldown(caster);
                RemComp<UnremoveableComponent>(uid);
                QueueDel(uid);
                return;
            }
        }

        if (!_mobs.IsAlive(target))
        {
            RemComp<UnremoveableComponent>(uid);
            QueueDel(uid);
            return;
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), caster);
        if (comp.CurrentPath == HereticPath.Blade && comp.ResearchedKnowledge.Contains("KnowledgeGraspOfBlade"))
        {
            if (IsBackstabTarget(caster, target))
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(2), true);
        }
        else
        {
            _stun.TryKnockdown(target, TimeSpan.FromSeconds(5), true);
        }

        foreach (var actionEnt in comp.GrantedActions)
        {
            if (TerminatingOrDeleted(actionEnt))
                continue;
            if (MetaData(actionEnt).EntityPrototype?.ID != "ActionHereticMansusGrasp")
                continue;
            _actions.SetCooldown(new Entity<ActionComponent?>(actionEnt, CompOrNull<ActionComponent>(actionEnt)), TimeSpan.FromSeconds(10));
            break;
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), target);
        Spawn(GetMansusMarkEffectProto(comp.CurrentPath), Transform(target).Coordinates);
        ApplyGraspMark(caster, comp, target);
        if (HasGraspUpgrade(comp))
            ApplyGraspUpgrade(caster, comp, target);

        RemComp<UnremoveableComponent>(uid);
        QueueDel(uid);
    }

    private static string GetMansusMarkEffectProto(HereticPath path)
    {
        return path switch
        {
            HereticPath.Ash    => "HereticEffectMansusMarkAsh",
            HereticPath.Lock   => "HereticEffectMansusMarkLock",
            HereticPath.Flesh  => "HereticEffectMansusMarkFlesh",
            HereticPath.Void   => "HereticEffectMansusMarkVoid",
            HereticPath.Blade  => "HereticEffectMansusMarkBlade",
            HereticPath.Rust   => "HereticEffectMansusMarkRust",
            HereticPath.Cosmos => "HereticEffectMansusMarkCosmos",
            _                  => "HereticEffectMansusMarkAsh",
        };
    }

    /// <summary>
    /// Grasp of the Blade: the stun only triggers if the target is prone or facing away from the heretic.
    /// </summary>
    private bool IsBackstabTarget(EntityUid heretic, EntityUid target)
    {
        if (_standing.IsDown(target))
            return true;

        var toHeretic = _xform.GetWorldPosition(heretic) - _xform.GetWorldPosition(target);
        if (toHeretic.LengthSquared() < 0.001f)
            return false;

        var approachAngle = Angle.FromWorldVec(toHeretic);
        var targetFacing = _xform.GetWorldRotation(target);
        var angleDiff = (approachAngle - targetFacing).Reduced().FlipPositive();

        return angleDiff > Math.PI / 2;
    }

    /// <summary>
    /// Mark of the Blade: first hit confines the target to their current room by bolting nearby
    /// doors; a follow-up hit on an already-marked target removes the mark, unbolts the doors and
    /// grants the heretic one orbiting blade.
    /// </summary>
    private const float BladeMarkLockRadius = 8f;

    private void ApplyBladeMark(EntityUid heretic, EntityUid target)
    {
        var mark = EnsureComp<HereticBladeMarkComponent>(target);
        mark.Heretic = heretic;
        mark.LockedDoors.Clear();

        var coords = Transform(target).Coordinates;
        foreach (var door in _lookup.GetEntitiesInRange<DoorBoltComponent>(coords, BladeMarkLockRadius))
        {
            if (_door.TrySetBoltDown(door, true))
                mark.LockedDoors.Add(door);
        }

        Dirty(target, mark);
        _popup.PopupEntity(Loc.GetString("heretic-grasp-mark-blade"), target, target, PopupType.SmallCaution);
    }

    // SS13: trigger_mark — consume existing mark, grant orbiting blade
    public bool TryTriggerBladeMark(EntityUid heretic, EntityUid target)
    {
        if (!TryComp<HereticBladeMarkComponent>(target, out var mark) || mark.Heretic != heretic)
            return false;
        RemoveBladeMark(target, mark);
        SpawnOrbitingBlade(heretic);
        _popup.PopupEntity(Loc.GetString("heretic-grasp-mark-blade-removed"), target, target, PopupType.MediumCaution);
        return true;
    }

    public bool TryTriggerLockMark(EntityUid heretic, EntityUid target)
    {
        if (!TryComp<LockMarkComponent>(target, out var lockMark))
            return false;

        if (lockMark.IdCard.HasValue && TryComp<AccessComponent>(lockMark.IdCard.Value, out var idAccess))
        {
            idAccess.Tags.Clear();
            _access.SetAccessEnabled(lockMark.IdCard.Value, true, idAccess);
            Dirty(lockMark.IdCard.Value, idAccess);
        }

        RemComp<LockMarkComponent>(target);
        _popup.PopupEntity(Loc.GetString("heretic-grasp-mark-lock-removed"), target, target, PopupType.LargeCaution);
        return true;
    }

    /// <summary>
    /// Empowered Blades: a regular knife hit (not the Mansus Grasp action) now strikes with both hands
    /// at once and, on cooldown, imbues the strike with the Mansus Grasp effect.
    /// </summary>
    public void TryEmpoweredBladesOnHit(EntityUid heretic, HereticComponent comp, EntityUid target, EntityUid weapon)
    {
        // SS13: on_blade_equipped → demolition_mod = 2.5 (base 1x already dealt, +1.5x bonus = 2.5x total)
        if (!HasComp<MobStateComponent>(target))
        {
            if (TryComp<MeleeWeaponComponent>(weapon, out var weaponMelee) && HasComp<DamageableComponent>(target))
            {
                var bonusDmg = weaponMelee.Damage * 1.5f;
                _damageSystem.TryChangeDamage(target, bonusDmg, ignoreResistances: true);
            }
            return;
        }

        if (!_mobs.IsAlive(target) || target == heretic)
            return;

        // SS13: afterattack — infused hit applies blade mark then de-infuses; backstab adds paralysis + brute
        if (TryComp<HereticBladeBladeComponent>(weapon, out var bladeComp) && bladeComp.Infused)
        {
            ApplyBladeMark(heretic, target);
            bladeComp.Infused = false;
            Dirty(weapon, bladeComp);
            _appearance.SetData(weapon, HereticBladeSunderedVisuals.Infused, false);

            if (IsBackstabTarget(heretic, target))
            {
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(1.5), true);
                var backstabDmg = new DamageSpecifier();
                backstabDmg.DamageDict["Blunt"] = FixedPoint2.New(10);
                _damageSystem.TryChangeDamage(target, backstabDmg, ignoreResistances: true);
                _popup.PopupEntity(Loc.GetString("heretic-blade-backstab"), heretic, heretic, PopupType.Medium);
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_weapons_guillotine.ogg"), heretic);
            }
        }

        // SS13: do_melee_effects — follow-up offhand attack with 0.25s delay (only when offhand blade exists)
        EntityUid? offhandBlade = null;
        foreach (var held in _hands.EnumerateHeld(heretic))
        {
            if (held == weapon) continue;
            if (!_tag.HasTag(held, HereticBladeTag)) continue;
            offhandBlade = held;
            break;
        }

        if (offhandBlade == null)
            return;

        var offhand = offhandBlade.Value;
        Timer.Spawn(TimeSpan.FromSeconds(0.25), () =>
        {
            if (TerminatingOrDeleted(heretic) || TerminatingOrDeleted(target) || TerminatingOrDeleted(offhand))
                return;
            if (!_mobs.IsAlive(target)) return;
            if (!TryComp<MeleeWeaponComponent>(offhand, out var offMelee)) return;

            var dmg = offMelee.Damage * 0.8f;
            _damageSystem.TryChangeDamage(target, dmg, ignoreResistances: false);
            if (offMelee.HitSound != null)
                _audio.PlayPvs(offMelee.HitSound, heretic);
        });
    }

    private void RemoveBladeMark(EntityUid target, HereticBladeMarkComponent mark)
    {
        foreach (var door in mark.LockedDoors)
        {
            if (Exists(door) && TryComp<DoorBoltComponent>(door, out var boltComp))
                _door.TrySetBoltDown((door, boltComp), false);
        }

        RemComp<HereticBladeMarkComponent>(target);
    }

    public void ApplyGraspMark(EntityUid heretic, HereticComponent comp, EntityUid target)
    {
        switch (comp.CurrentPath)
        {
            case HereticPath.Ash:
            {
                _blindable.AdjustEyeDamage((target, null), 9);
                _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(
                    target, TemporaryBlindnessSystem.BlindingStatusEffect, TimeSpan.FromSeconds(20), true);
                _flammable.AdjustFireStacks(target, 3f, ignite: true);
                EnsureComp<AshMarkComponent>(target);
                break;
            }
            case HereticPath.Moon:
            {
                // Скрыть еретика на 5 секунд (moon_grasp_hide из SS13)
                var moonStealth = EnsureComp<StealthComponent>(heretic);
                _stealth.SetEnabled(heretic, true, moonStealth);
                _stealth.SetVisibility(heretic, -1f, moonStealth);
                if (_graspLunacyCancels.Remove(heretic, out var oldMoonCts))
                    oldMoonCts.Cancel();
                var moonCts = new CancellationTokenSource();
                _graspLunacyCancels[heretic] = moonCts;
                var capturedHeretic = heretic;
                Timer.Spawn(TimeSpan.FromSeconds(5), () =>
                {
                    if (Deleted(capturedHeretic)) return;
                    _graspLunacyCancels.Remove(capturedHeretic);
                    if (TryComp<StealthComponent>(capturedHeretic, out var sc) && sc.Enabled)
                        _stealth.SetEnabled(capturedHeretic, false, sc);
                }, moonCts.Token);

                // Цель: галлюцинации 20 сек + -30 рассудка
                _hereticEffects.ApplyHallucination(target, TimeSpan.FromSeconds(20));
                _moonBrainDamage.AddBrainDamage(target, 30f);
                EnsureComp<MoonMarkComponent>(target);
                _popup.PopupEntity(Loc.GetString("heretic-grasp-mark-moon"), target, target, PopupType.SmallCaution);
                break;
            }
            case HereticPath.Lock:
            {
                var lockMark = EnsureComp<LockMarkComponent>(target);
                if (_inventory.TryGetSlotEntity(target, "id", out var idSlotItem))
                {
                    var cardId = idSlotItem.Value;
                    if (TryComp<PdaComponent>(idSlotItem, out var pda) && pda.ContainedId.HasValue)
                        cardId = pda.ContainedId.Value;

                    if (TryComp<AccessComponent>(cardId, out var idAccess))
                    {
                        lockMark.IdCard = cardId;
                        _access.SetAccessEnabled(cardId, false, idAccess);
                    }
                }
                _popup.PopupEntity(Loc.GetString("heretic-grasp-mark-lock"), target, target, PopupType.SmallCaution);
                break;
            }
            case HereticPath.Void:
                if (TryComp<StatusEffectsComponent>(target, out var voidSe))
                    _statusEffects.TryAddStatusEffect<MutedComponent>(target, "Muted", TimeSpan.FromSeconds(10), true, voidSe);
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(3), true);
                var voidGraspDmg = new DamageSpecifier();
                voidGraspDmg.DamageDict["Blunt"] = FixedPoint2.New(10);
                _damageSystem.TryChangeDamage(target, voidGraspDmg, ignoreResistances: false);
                _hereticEffects.ApplyVoidChill(target, 2);
                EnsureComp<VoidMarkComponent>(target);
                _popup.PopupEntity(Loc.GetString("heretic-grasp-mark-void"), target, target, PopupType.SmallCaution);
                break;
            case HereticPath.Blade:
                if (comp.ResearchedKnowledge.Contains("KnowledgeSanguineSurge"))
                    _bloodstream.TryModifyBleedAmount(target, 5f);
                if (comp.ResearchedKnowledge.Contains("KnowledgeMarkOfBlade"))
                    ApplyBladeMark(heretic, target);
                break;
            case HereticPath.Rust when comp.ResearchedKnowledge.Contains("KnowledgeCorrode"):
            {
                var dmg = new DamageSpecifier();
                dmg.DamageDict["Caustic"] = FixedPoint2.New(10);
                _damageSystem.TryChangeDamage(target, dmg, ignoreResistances: false);

                if (comp.ResearchedKnowledge.Contains("KnowledgeMarkOfRust") && !HasComp<RustMarkComponent>(target))
                {
                    EnsureComp<RustMarkComponent>(target);
                    _popup.PopupEntity(Loc.GetString("heretic-grasp-mark-rust"), target, target, PopupType.SmallCaution);
                }
                break;
            }
            case HereticPath.Cosmos:
            {
                if (HasComp<HereticComponent>(target) || HasComp<CosmosMarkImmuneComponent>(target))
                    break;
                var mark = EnsureComp<CosmosMarkComponent>(target);
                if (mark.AnchorEntity.HasValue && !TerminatingOrDeleted(mark.AnchorEntity.Value))
                    QueueDel(mark.AnchorEntity.Value);
                mark.AnchorEntity = Spawn("HereticCosmicDiamondAnchor", Transform(target).Coordinates);
                var capturedTarget = target;
                var capturedAnchor = mark.AnchorEntity.Value;
                Timer.Spawn(TimeSpan.FromSeconds(15), () =>
                {
                    if (Deleted(capturedTarget) || !TryComp<CosmosMarkComponent>(capturedTarget, out var m)) return;
                    if (m.AnchorEntity == capturedAnchor)
                    {
                        if (!TerminatingOrDeleted(capturedAnchor))
                            QueueDel(capturedAnchor);
                        RemCompDeferred<CosmosMarkComponent>(capturedTarget);
                    }
                });
                _popup.PopupEntity(Loc.GetString("heretic-grasp-mark-cosmos"), target, target, PopupType.SmallCaution);
                break;
            }
        }
    }

    private bool HasGraspUpgrade(HereticComponent comp)
    {
        return comp.CurrentPath switch
        {
            HereticPath.Ash    => comp.ResearchedKnowledge.Contains("KnowledgeAshenPassage"),
            HereticPath.Lock   => false,
            HereticPath.Flesh  => false,
            HereticPath.Void   => false,
            HereticPath.Blade  => comp.ResearchedKnowledge.Contains("KnowledgeCleave"),
            HereticPath.Rust   => comp.ResearchedKnowledge.Contains("KnowledgeRustWave"),
            HereticPath.Cosmos => false,
            _                   => false,
        };
    }

    private void ApplyGraspUpgrade(EntityUid heretic, HereticComponent comp, EntityUid target)
    {
        _stun.TryAddStunDuration(target, TimeSpan.FromSeconds(2));

        switch (comp.CurrentPath)
        {
            case HereticPath.Ash:
            {
                var dmg = new DamageSpecifier();
                dmg.DamageDict["Heat"] = FixedPoint2.New(15);
                _damageSystem.TryChangeDamage(target, dmg, ignoreResistances: true);
                break;
            }
            case HereticPath.Blade:
            {
                var dmg = new DamageSpecifier();
                dmg.DamageDict["Slash"] = FixedPoint2.New(15);
                _damageSystem.TryChangeDamage(target, dmg, ignoreResistances: false);
                _bloodstream.TryModifyBleedAmount(target, 10f);
                break;
            }
            case HereticPath.Rust:
            {
                var dmg = new DamageSpecifier();
                dmg.DamageDict["Caustic"] = FixedPoint2.New(15);
                _damageSystem.TryChangeDamage(target, dmg, ignoreResistances: false);
                break;
            }
        }
    }

    // ─── Blade management ────────────────────────────────────────────────────

    public void IncrementSunderedBladeCraft(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp)) return;
        comp.SunderedBladeCraftCount++;
        Dirty(uid, comp);
    }

    public bool CanCraftSunderedBlade(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp)) return false;
        return comp.SunderedBladeCraftCount < 5;
    }

    public void IncrementKeyBladeCraft(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp)) return;
        comp.KeyBladeCraftCount++;
        Dirty(uid, comp);
    }

    public bool CanCraftKeyBlade(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp)) return false;
        return comp.KeyBladeCraftCount < 2;
    }

    public void IncrementShatteredGhoulCraft(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp)) return;
        comp.ShatteredGhoulCount++;
        Dirty(uid, comp);
    }

    public bool CanCraftShatteredGhoul(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp)) return false;
        return comp.ShatteredGhoulCount < 1;
    }

    // ─── Rust tiles (shared by Grasp of Rust / Leeching Walk / etc.) ───────

    public bool IsTileRusted(EntityCoordinates coordinates)
    {
        var gridUid = _xform.GetGrid(coordinates);
        if (gridUid is not { } grid) return false;
        var snapped = coordinates.SnapToGrid(EntityManager);
        foreach (var (_, decal) in _decal.GetDecalsInRange(grid, snapped.Position))
        {
            if (decal.Id == RustDecalId) return true;
        }
        var mapCoords = _xform.ToMapCoordinates(snapped);
        return _lookup.GetEntitiesInRange<HereticRustOverlayComponent>(mapCoords, 0.4f).Count > 0;
    }

    public void RustTile(EntityCoordinates coordinates)
    {
        if (IsTileRusted(coordinates)) return;
        var snapped = coordinates.SnapToGrid(EntityManager);
        _decal.TryAddDecal(RustDecalId, snapped, out _);
    }

    /// <summary>
    /// Spreads rust to every tile in range, and destroys any HereticRustWall entities in range.
    /// </summary>
    public void SpreadRustNearby(EntityCoordinates origin, float radius)
    {
        var gridUid = _xform.GetGrid(origin);
        if (gridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var worldPos = _xform.ToMapCoordinates(origin).Position;
        var circle = new Circle(worldPos, radius);
        foreach (var tile in _mapSystem.GetTilesIntersecting(grid, gridComp, circle))
        {
            RustTile(_mapSystem.GridTileToLocal(grid, gridComp, tile.GridIndices));
        }

        foreach (var wall in _lookup.GetEntitiesInRange(origin, radius).ToList())
        {
            var protoId = MetaData(wall).EntityPrototype?.ID;
            if (protoId == "HereticRustWall")
            {
                QueueDel(wall);
            }
            else if (protoId is "WallSolid" or "WallReinforced")
            {
                var wallCoords = Transform(wall).Coordinates;
                QueueDel(wall);
                Spawn(protoId == "WallSolid" ? "WallSolidRust" : "WallReinforcedRust", wallCoords);
            }
        }
    }

    // SS13-identical wave: rust covers the whole station tile by tile, ring by ring (Chebyshev).
    // Ring d fires after 2*d seconds; each ring is shuffled and split into thirds staggered over 5 seconds.
    public void TriggerRustAscensionWave(EntityCoordinates origin)
    {
        var gridUid = _xform.GetGrid(origin);
        if (gridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        var worldOrigin = _xform.ToMapCoordinates(origin).Position;
        var byDistance  = new Dictionary<int, List<Vector2i>>();

        foreach (var tile in _mapSystem.GetAllTiles(grid, gridComp))
        {
            var tileCoords = _mapSystem.GridTileToLocal(grid, gridComp, tile.GridIndices);
            var tileWorld  = _xform.ToMapCoordinates(tileCoords).Position;
            var dx   = (int)MathF.Round(MathF.Abs(tileWorld.X - worldOrigin.X));
            var dy   = (int)MathF.Round(MathF.Abs(tileWorld.Y - worldOrigin.Y));
            var dist = Math.Max(dx, dy);

            if (!byDistance.TryGetValue(dist, out var list))
            {
                list = new List<Vector2i>();
                byDistance[dist] = list;
            }
            list.Add(tile.GridIndices);
        }

        foreach (var (dist, indices) in byDistance)
        {
            var ringDelay     = 2000 * dist;
            var capturedGrid  = grid;
            var capturedTiles = new List<Vector2i>(indices);

            Timer.Spawn(ringDelay, () =>
            {
                if (!Exists(capturedGrid) || !TryComp<MapGridComponent>(capturedGrid, out var gc)) return;

                _random.Shuffle(capturedTiles);
                var count = capturedTiles.Count;
                var third = Math.Max(1, count / 3);

                for (var i = 0; i < count; i++)
                {
                    var idx       = capturedTiles[i];
                    var staggerMs = i < third       ? 1650
                                  : i < third * 2   ? 3300
                                                    : 5000;

                    Timer.Spawn(staggerMs, () =>
                    {
                        if (!Exists(capturedGrid) || !TryComp<MapGridComponent>(capturedGrid, out var gcInner)) return;
                        var coords  = _mapSystem.GridTileToLocal(capturedGrid, gcInner, idx);
                        RustTile(coords);
                        Spawn("HereticRustOverlay", coords);
                        foreach (var wall in _lookup.GetEntitiesInRange(coords, 0.6f).ToList())
                        {
                            if (TerminatingOrDeleted(wall)) continue;
                            var protoId = MetaData(wall).EntityPrototype?.ID;
                            if (protoId is not ("WallSolid" or "WallReinforced")) continue;
                            var wallPos = Transform(wall).Coordinates;
                            QueueDel(wall);
                            Spawn(protoId == "WallSolid" ? "WallSolidRust" : "WallReinforcedRust", wallPos);
                        }
                        var offsetX = (_random.NextFloat() * 2f - 1f) * 0.1875f;
                        var offsetY = (_random.NextFloat() * 2f - 1f) * 0.1875f;
                        var runeId  = RustAscensionRuneEffects[_random.Next(RustAscensionRuneEffects.Length)];
                        Spawn(runeId, new EntityCoordinates(coords.EntityId, coords.Position + new Vector2(offsetX, offsetY)));
                    });
                }
            });
        }
    }

    // ─── Orbiting blades (Mark of the Blade / Furious Steel) ───────────────

    public EntityUid SpawnOrbitingBlade(EntityUid uid, HereticComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return EntityUid.Invalid;

        var index = comp.OrbitingBlades.Count;
        var blade = Spawn("HereticOrbitingBlade", Transform(uid).Coordinates);
        _xform.SetParent(blade, uid);
        var orbitComp = EnsureComp<HereticOrbitingBladeComponent>(blade);
        orbitComp.OwnerHeretic = uid;
        var orbitVisuals = EnsureComp<OrbitVisualsComponent>(blade);
        orbitVisuals.PhaseOffset = index / 3f;
        Dirty(blade, orbitVisuals);
        comp.OrbitingBlades.Add(blade);
        comp.BladesCreated++;
        Dirty(uid, comp);
        return blade;
    }

    public bool TryConsumeOrbitingBlade(EntityUid uid, HereticComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return false;

        while (comp.OrbitingBlades.Count > 0)
        {
            var blade = comp.OrbitingBlades[^1];
            comp.OrbitingBlades.RemoveAt(comp.OrbitingBlades.Count - 1);
            if (!Exists(blade))
                continue;

            Del(blade);
            Dirty(uid, comp);
            return true;
        }

        return false;
    }

    public void ClearOrbitingBlades(EntityUid uid, HereticComponent? comp = null)
    {
        if (!Resolve(uid, ref comp))
            return;

        foreach (var blade in comp.OrbitingBlades)
        {
            if (Exists(blade))
                Del(blade);
        }

        comp.OrbitingBlades.Clear();
        Dirty(uid, comp);
    }

    public void UpgradeBlade(EntityUid uid)
    {
        if (!TryComp<HereticComponent>(uid, out var comp)) return;
        if (comp.CurrentPath == HereticPath.General) return;
        ApplyBladeUpgrade(uid, comp);
        comp.BladeUpgraded = true;
        Dirty(uid, comp);
    }

    private void SpawnBlade(EntityUid uid, HereticComponent comp, string bladeEntityId)
    {
        if (comp.CurrentBlade != EntityUid.Invalid && Exists(comp.CurrentBlade))
            Del(comp.CurrentBlade);

        comp.CurrentBlade = EntityUid.Invalid;
        var blade = Spawn(bladeEntityId, Transform(uid).Coordinates);
        _hands.TryPickupAnyHand(uid, blade);
        comp.CurrentBlade = blade;
        Dirty(uid, comp);
    }

    private void AddPathBladeComponent(EntityUid uid, HereticComponent comp, EntityUid blade)
    {
        if (!Exists(blade)) return;
        switch (comp.CurrentPath)
        {
            case HereticPath.Ash:    EnsureComp<HereticAshBladeComponent>(blade); break;
            case HereticPath.Lock:   EnsureComp<HereticLockBladeComponent>(blade); break;
            case HereticPath.Void:   EnsureComp<HereticVoidBladeComponent>(blade); break;
            case HereticPath.Blade:  EnsureComp<HereticBladeBladeComponent>(blade); break;
            case HereticPath.Rust:   EnsureComp<HereticRustBladeComponent>(blade); break;
            case HereticPath.Cosmos: EnsureComp<HereticCosmosBladeComponent>(blade); break;
            case HereticPath.Flesh:  EnsureComp<HereticFleshBladeComponent>(blade); break;
            case HereticPath.Moon:   EnsureComp<HereticMoonBladeComponent>(blade); break;
        }
    }

    private EntityUid FindHeldPathBlade(EntityUid uid, HereticPath path)
    {
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            var isPathBlade = path switch
            {
                HereticPath.Ash    => HasComp<HereticAshBladeComponent>(held),
                HereticPath.Lock   => HasComp<HereticLockBladeComponent>(held),
                HereticPath.Void   => HasComp<HereticVoidBladeComponent>(held),
                HereticPath.Blade  => HasComp<HereticBladeBladeComponent>(held),
                HereticPath.Rust   => HasComp<HereticRustBladeComponent>(held),
                HereticPath.Cosmos => HasComp<HereticCosmosBladeComponent>(held),
                HereticPath.Flesh  => HasComp<HereticFleshBladeComponent>(held),
                HereticPath.Moon   => HasComp<HereticMoonBladeComponent>(held),
                _                  => false,
            };
            if (isPathBlade) return held;
        }
        return EntityUid.Invalid;
    }

    private void ApplyBladeUpgrade(EntityUid uid, HereticComponent comp)
    {
        EntityUid blade;
        if (comp.CurrentBlade != EntityUid.Invalid && Exists(comp.CurrentBlade))
        {
            blade = comp.CurrentBlade;
        }
        else
        {
            blade = FindHeldPathBlade(uid, comp.CurrentPath);
            if (blade == EntityUid.Invalid)
            {
                // Fallback: find any held item with the HereticBlade tag (e.g. HereticBladeBase from ritual)
                foreach (var held in _hands.EnumerateHeld(uid))
                {
                    if (_tag.HasTag(held, HereticBladeTag))
                    {
                        blade = held;
                        break;
                    }
                }
                if (blade == EntityUid.Invalid) return;
                AddPathBladeComponent(uid, comp, blade);
            }
            comp.CurrentBlade = blade;
            Dirty(uid, comp);
        }
        if (!TryComp<MeleeWeaponComponent>(blade, out var melee)) return;

        switch (comp.CurrentPath)
        {
            case HereticPath.Ash:
                melee.Damage.DamageDict["Heat"] = FixedPoint2.New(12);
                var ignite = EnsureComp<IgniteOnMeleeHitComponent>(blade);
                ignite.FireStacks = 1.0f;
                break;
            case HereticPath.Lock:
                melee.Damage.DamageDict["Blunt"] = FixedPoint2.New(10);
                melee.Damage.DamageDict["Bloodloss"] = FixedPoint2.New(5);
                break;
            case HereticPath.Flesh:
            {
                var fleshPos = Transform(blade).Coordinates;
                var fleshBlade = Spawn("HereticBladeFlesh", fleshPos);
                EnsureComp<HereticFleshBladeComponent>(fleshBlade);
                Del(blade);
                comp.CurrentBlade = fleshBlade;
                Dirty(uid, comp);
                _hands.TryPickupAnyHand(uid, fleshBlade);
                if (TryComp<MeleeWeaponComponent>(fleshBlade, out var fleshMelee))
                {
                    fleshMelee.Damage.DamageDict["Slash"] = FixedPoint2.New(22);
                    fleshMelee.Damage.DamageDict["Piercing"] = FixedPoint2.New(8);
                    fleshMelee.Damage.DamageDict["Bloodloss"] = FixedPoint2.New(8);
                    Dirty(fleshBlade, fleshMelee);
                }
                return;
            }
            case HereticPath.Void:
                melee.Damage.DamageDict["Cold"] = FixedPoint2.New(12);
                break;
            case HereticPath.Blade:
                melee.Damage.DamageDict["Slash"] = FixedPoint2.New(28);
                melee.AttackRate = 2.0f;
                break;
            case HereticPath.Rust:
                melee.Damage.DamageDict["Caustic"] = FixedPoint2.New(12);
                break;
            case HereticPath.Cosmos:
            {
                var cosmosPos = Transform(blade).Coordinates;
                var cosmosBlade = Spawn("HereticBladeCosmos", cosmosPos);
                EnsureComp<HereticCosmosBladeComponent>(cosmosBlade);
                Del(blade);
                comp.CurrentBlade = cosmosBlade;
                Dirty(uid, comp);
                _hands.TryPickupAnyHand(uid, cosmosBlade);
                if (TryComp<MeleeWeaponComponent>(cosmosBlade, out var cosmelee))
                {
                    cosmelee.Damage.DamageDict["Radiation"] = FixedPoint2.New(12);
                    Dirty(cosmosBlade, cosmelee);
                }
                return;
            }
        }

        Dirty(blade, melee);
    }

    // ─── Passive knowledge effects ────────────────────────────────────────────

    private void ApplyPassiveKnowledgeEffect(EntityUid uid, HereticComponent comp, string knowledgeId)
    {
        DamageSpecifier? heal = null;
        string? popupKey = null;

        switch (knowledgeId)
        {
            case "KnowledgeBreakOfDawn":
                break; // Items are spawned directly in MakeHeretic

            case "KnowledgeLockwielder":
            {
                heal = new DamageSpecifier();
                heal.DamageDict["Blunt"]    = FixedPoint2.New(-20);
                heal.DamageDict["Slash"]    = FixedPoint2.New(-10);
                break;
            }

            case "KnowledgeFleshArtisan":
                heal = new DamageSpecifier();
                heal.DamageDict["Slash"]    = FixedPoint2.New(-25);
                heal.DamageDict["Piercing"] = FixedPoint2.New(-15);
                popupKey = "heretic-passive-flesh-artisan";
                break;

            case "KnowledgeVoidTraveler":
                heal = new DamageSpecifier();
                heal.DamageDict["Cold"]     = FixedPoint2.New(-15);
                heal.DamageDict["Cellular"] = FixedPoint2.New(-10);
                popupKey = "heretic-passive-void-traveler";
                break;

            case "KnowledgeBladeAdept":
                heal = new DamageSpecifier();
                heal.DamageDict["Slash"]    = FixedPoint2.New(-20);
                heal.DamageDict["Blunt"]    = FixedPoint2.New(-10);
                popupKey = "heretic-passive-blade-adept";
                break;

            case "KnowledgeRustReaper":
            {
                heal = new DamageSpecifier();
                heal.DamageDict["Caustic"]  = FixedPoint2.New(-15);
                heal.DamageDict["Slash"]    = FixedPoint2.New(-10);
                break;
            }

            case "KnowledgeCosmicAcolyte":
            {
                heal = new DamageSpecifier();
                heal.DamageDict["Radiation"] = FixedPoint2.New(-15);
                heal.DamageDict["Heat"]      = FixedPoint2.New(-10);
                var coords = Transform(uid).Coordinates;
                var spaceHands = Spawn("HereticSpaceHands", coords);
                _hands.TryPickupAnyHand(uid, spaceHands);
                popupKey = "heretic-space-hands-obtained";
                break;
            }

            case "KnowledgeGraspOfLunacy":
            {
                popupKey = "heretic-passive-grasp-of-lunacy";
                break;
            }

            case "KnowledgeVolcanoBlast":
            {
                if (TryComp<FlammableComponent>(uid, out var flammable))
                    flammable.MaximumFireStacks = 0f;
                popupKey = "heretic-passive-volcano-blast";
                break;
            }

            // ── Blade upgrades ───────────────────────────────────────────────
            case "KnowledgeFieryBlade":
            case "KnowledgeCravingBlade":
            case "KnowledgeBleedingSteel":
            case "KnowledgeToxicBlade":
            case "KnowledgeCosmicBlade":
            case "KnowledgeMoonBlade":
                ApplyBladeUpgrade(uid, comp);
                comp.BladeUpgraded = true;
                popupKey = "heretic-blade-upgrade-ritual";
                break;

            case "KnowledgeEmpoweredBlades":
                ApplyBladeUpgrade(uid, comp);
                comp.BladeUpgraded = true;
                popupKey = "heretic-passive-empowered-blades-learned";
                break;

            case "KnowledgeMawedCrucible":
            case "KnowledgeShopMawedCrucible":
                popupKey = "heretic-mawed-crucible-unlocked";
                break;

            case "KnowledgeFuriousSteel":
                popupKey = "heretic-passive-furious-steel";
                break;

            case "KnowledgeCallOfMoon":
                EnsureComp<HereticMoonBrainDamageComponent>(uid);
                popupKey = "heretic-passive-call-of-moon";
                break;

        }

        if (heal != null)
            _damageSystem.TryChangeDamage(uid, heal, ignoreResistances: true);

        if (popupKey != null)
            _popup.PopupEntity(Loc.GetString(popupKey), uid, uid, PopupType.Medium);
    }

    // ─── Passive knowledge gain ───────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.PassiveGainAccumulator += frameTime;
            if (comp.PassiveGainAccumulator >= comp.PassiveGainIntervalSeconds)
            {
                comp.PassiveGainAccumulator = 0f;
                comp.KnowledgePoints++;
                _popup.PopupEntity(Loc.GetString("heretic-passive-knowledge-gain"), uid, uid, PopupType.Small);
                var drain = DrainMessages[_random.Next(DrainMessages.Length)];
                SendHereticMessage(uid, Loc.GetString("heretic-passive-whisper", ("drain", drain)));

                // Amber Focus: extra knowledge point each tick
                foreach (var held in _hands.EnumerateHeld(uid))
                {
                    if (HasComp<HereticAmberFocusComponent>(held))
                    {
                        comp.KnowledgePoints++;
                        _popup.PopupEntity(Loc.GetString("heretic-amber-focus-bonus"), uid, uid, PopupType.Small);
                        break;
                    }
                }

                // Amber Focus: also check head slot (e.g. HereticRustArmorHood)
                if (_inventory.TryGetSlotEntity(uid, "head", out var headItem) &&
                    headItem != null &&
                    HasComp<HereticAmberFocusComponent>(headItem.Value))
                {
                    comp.KnowledgePoints++;
                    _popup.PopupEntity(Loc.GetString("heretic-amber-focus-bonus"), uid, uid, PopupType.Small);
                }

                SendInfoBuiState(uid, comp);
                Dirty(uid, comp);
            }
        }

        var bookQuery = EntityQueryEnumerator<HereticMansusBookComponent>();
        while (bookQuery.MoveNext(out var bookUid, out var bookComp))
        {
            if (bookComp.NextState == null)
                continue;

            bookComp.TransitionTimeRemaining -= frameTime;
            if (bookComp.TransitionTimeRemaining <= 0f)
            {
                _appearance.SetData(bookUid, HereticMansusBookVisuals.State, bookComp.NextState.Value);
                bookComp.NextState = null;
            }
        }
    }

    public bool TryGetRoundEndData(EntityUid playerUid, out HereticRoundEndData data)
    {
        if (!TryComp<HereticComponent>(playerUid, out var comp))
        {
            data = default;
            return false;
        }
        data = new HereticRoundEndData(
            comp.CurrentPath,
            comp.SacrificeCount,
            comp.RequiredSacrifices,
            comp.HighValueSacrificeCount,
            comp.TotalKnowledgeGained,
            comp.RequiredKnowledge,
            comp.AscensionTriggered,
            comp.ResearchedKnowledge
        );
        return true;
    }
}

public record struct HereticRoundEndData(
    HereticPath CurrentPath,
    int SacrificeCount,
    int RequiredSacrifices,
    int HighValueSacrificeCount,
    int TotalKnowledgeGained,
    int RequiredKnowledge,
    bool AscensionTriggered,
    IReadOnlyList<ProtoId<HereticKnowledgePrototype>> ResearchedKnowledge
);
