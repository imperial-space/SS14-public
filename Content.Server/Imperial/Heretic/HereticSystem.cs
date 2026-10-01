using System.Numerics;
using System.Text;
using Content.Server.Actions;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Decals;
using Content.Server.Doors.Systems;
using Content.Server.Imperial.Antimagic;
using Content.Server.Imperial.Heretic.Effects;
using Content.Server.Imperial.Heretic.Paths.Ash;
using Content.Server.Imperial.Heretic.Paths.Blade;
using Content.Server.Imperial.Heretic.Paths.Cosmos;
using Content.Server.Imperial.Heretic.Paths.Lock;
using Content.Server.Imperial.Heretic.Paths.Moon;
using Content.Server.Imperial.Heretic.Paths.Rust;
using Content.Server.Imperial.Heretic.Paths.Void;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared.Access.Systems;
using Content.Shared.Actions.Components;
using Content.Shared.Antag;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Follower.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Imperial.Antimagic;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Objectives.Systems;
using Content.Shared.Overlays;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.Stealth;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Heretic;

public sealed partial class HereticSystem : SharedHereticSystem
{
    private static readonly ProtoId<TagPrototype> HereticBladeTag = "HereticBlade";

    private static readonly string[] HereticGradientHex =
    {
        "#3A00C0", "#6B14D4", "#9433E8", "#B85AF0", "#D080F8", "#E0AAFF",
        "#D080F8", "#B85AF0", "#9433E8", "#6B14D4",
    };

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly RoleSystem _role = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedStutteringSystem _stuttering = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly DamageableSystem _damageSystem = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly HereticStatusEffectsSystem _hereticEffects = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly BlindableSystem _blindable = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly DoorSystem _door = default!;
    [Dependency] private readonly SharedMechSystem _mech = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DecalSystem _decal = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedChatSystem _chat = default!;
    [Dependency] private readonly ChatSystem _chatServer = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedAccessSystem _access = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly HereticMarkSystem _marks = default!;
    [Dependency] private readonly HereticMoonBrainDamageSystem _moonBrainDamage = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly HereticAshPassiveSystem _ashPassive = default!;
    [Dependency] private readonly HereticMoonPassiveSystem _moonPassive = default!;
    [Dependency] private readonly HereticLockPassiveSystem _lockPassive = default!;
    [Dependency] private readonly HereticBladePassiveSystem _bladePassive = default!;
    [Dependency] private readonly HereticVoidPassiveSystem _voidPassive = default!;
    [Dependency] private readonly HereticRustPassiveSystem _rustPassive = default!;
    [Dependency] private readonly HereticCosmosPassiveSystem _cosmosPassive = default!;
    [Dependency] private readonly ImperialAntimagicSystem _antimagic = default!;

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

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticComponent, HereticKnowledgeMenuActionEvent>(OnKnowledgeMenu);
        SubscribeLocalEvent<HereticComponent, HereticMansusGraspActionEvent>(OnMansusGrasp);
        SubscribeLocalEvent<HereticMansusGraspItemComponent, AfterInteractEvent>(OnMansusGraspItemAfterInteract);
        SubscribeLocalEvent<HereticMansusGraspItemComponent, SuicideByEnvironmentEvent>(OnMansusGraspItemSuicide);
        SubscribeLocalEvent<HereticComponent, ImperialMagicCastAttemptEvent>(OnMagicCastAttempt);
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
        var removedEv = new HereticRemovedEvent(uid);
        RaiseLocalEvent(ref removedEv);
        RemCompDeferred<HereticGraspLunacyStealthComponent>(uid);

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
        var gradientMessage = ApplyHereticGradient(message);
        var wrapped = $"[bold]{gradientMessage}[/bold]";
        _chatManager.ChatMessageToOne(ChatChannel.Server, message, wrapped, default, false, actor.PlayerSession.Channel);
    }

    private static string ApplyHereticGradient(string message)
    {
        var sb = new StringBuilder(message.Length * 20);
        var i = 0;
        foreach (var ch in message)
        {
            var hex = HereticGradientHex[i % HereticGradientHex.Length];
            i++;
            sb.Append($"[color={hex}]{ch}[/color]");
        }
        return sb.ToString();
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
        if (!TryComp<HereticComponent>(uid, out var comp))
            return;
        comp.AscensionBypass = value;
        Dirty(uid, comp);
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
    // ─── Passive knowledge gain ───────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateGraspLunacy();

        // AllEntityQuery: тело еретика в другой форме лежит на карте-хранилище на паузе, но знания копятся.
        var query = AllEntityQuery<HereticComponent>();
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
