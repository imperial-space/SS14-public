using System.Linq;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Server.DoAfter;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Revolutionary.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Clothing.Components;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Body;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Imperial.Heretic.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Item;
using Content.Shared.Stacks;
using Content.Server.Imperial.Heretic.Components;
using Content.Shared.Imperial.Cult.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Tag;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticRuneSystem : EntitySystem
{
    [Dependency] private readonly IChatManager               _chatManager  = default!;
    [Dependency] private readonly DamageableSystem           _damageable   = default!;
    [Dependency] private readonly DoAfterSystem              _doAfter      = default!;
    [Dependency] private readonly EntityLookupSystem         _lookup       = default!;
    [Dependency] private readonly HereticSystem               _heretic      = default!;
    [Dependency] private readonly HereticAshPassiveSystem      _ashPassive    = default!;
    [Dependency] private readonly HereticMoonPassiveSystem     _moonPassive   = default!;
    [Dependency] private readonly HereticFleshPassiveSystem    _fleshPassive  = default!;
    [Dependency] private readonly HereticVoidPassiveSystem     _voidPassive   = default!;
    [Dependency] private readonly HereticRustPassiveSystem     _rustPassive     = default!;
    [Dependency] private readonly HereticCosmosPassiveSystem   _cosmosPassive   = default!;
    [Dependency] private readonly HereticFeastOfOwlsSystem   _feastOfOwls   = default!;
    [Dependency] private readonly HereticWarrenKingGreetingSystem _warrenKing = default!;
    [Dependency] private readonly MindSystem                       _mind       = default!;
    [Dependency] private readonly IPrototypeManager          _proto        = default!;
    [Dependency] private readonly MobStateSystem             _mobs         = default!;
    [Dependency] private readonly MobThresholdSystem         _mobThresholds = default!;
    [Dependency] private readonly NpcFactionSystem            _faction      = default!;
    [Dependency] private readonly PopupSystem                _popup        = default!;
    [Dependency] private readonly SharedAudioSystem          _audio        = default!;
    [Dependency] private readonly SharedHandsSystem          _hands        = default!;
    [Dependency] private readonly SharedItemSystem           _item         = default!;
    [Dependency] private readonly SharedStackSystem          _stack        = default!;
    [Dependency] private readonly SharedTransformSystem      _xform        = default!;
    [Dependency] private readonly SharedVisualBodySystem      _visualBody   = default!;
    [Dependency] private readonly AtmosphereSystem           _atmosphere   = default!;
    [Dependency] private readonly GibbingSystem              _gibbing      = default!;
    [Dependency] private readonly SharedJitteringSystem      _jitter       = default!;
    [Dependency] private readonly TagSystem                  _tag          = default!;
    [Dependency] private readonly UserInterfaceSystem        _ui           = default!;

    private const string FamiliarFaction = "HereticFamiliar";
    private const float DrawTime     = 22.0f; // full cycle of transmutation_rune_draw_colour
    private const float DrawTimeFast =  8.2f; // full active portion of transmutation_rune_fast_colour (before 20s hold)

    private readonly Dictionary<EntityUid, EntityUid> _drawEffects = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticComponent, DrawHereticRuneDoAfterEvent>(OnDrawRuneDoAfter);
        SubscribeLocalEvent<HereticComponent, HereticStartSlowRuneDrawEvent>(OnStartSlowRuneDraw);
        SubscribeLocalEvent<HereticComponent, AbsorbRiftCodexDoAfterEvent>(OnAbsorbRiftCodexDoAfter);
        SubscribeLocalEvent<HereticMansusGraspItemComponent, UseInHandEvent>(OnGraspUseInHand);
        SubscribeLocalEvent<HereticCodexComponent, UseInHandEvent>(OnCodexUseInHand);
        SubscribeLocalEvent<HereticCodexComponent, AfterInteractEvent>(OnCodexAfterInteract);
        SubscribeLocalEvent<HereticRuneComponent, ActivateInWorldEvent>(OnRuneActivated);
        SubscribeLocalEvent<HereticRuneComponent, HereticSelectRitualMessage>(OnRitualSelected);
        SubscribeLocalEvent<HereticRuneComponent, BoundUserInterfaceCheckRangeEvent>(OnRuneBuiRangeCheck);
        SubscribeLocalEvent<HereticGhoulComponent, PickupAttemptEvent>(OnGhoulPickupAttempt);
    }

    private void OnGhoulPickupAttempt(Entity<HereticGhoulComponent> ent, ref PickupAttemptEvent args)
    {
        args.Cancel();
    }

    // ─── Rune Drawing ────────────────────────────────────────────────────────

    private void OnStartSlowRuneDraw(EntityUid uid, HereticComponent comp, HereticStartSlowRuneDrawEvent args)
    {
        StartRuneDraw(uid, DrawTime);
    }

    private void OnGraspUseInHand(EntityUid uid, HereticMansusGraspItemComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        if (!TryComp<HereticComponent>(user, out _))
            return;

        args.Handled = true;
        StartRuneDraw(user, DrawTime);
    }

    private void OnCodexUseInHand(EntityUid uid, HereticCodexComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        if (!TryComp<HereticComponent>(user, out _))
            return;

        args.Handled = true;
        comp.IsOpen = !comp.IsOpen;
        Dirty(uid, comp);
        _item.SetSize(uid, comp.IsOpen ? "Huge" : "Small");

        var key = comp.IsOpen ? "heretic-codex-opened" : "heretic-codex-closed";
        _popup.PopupEntity(Loc.GetString(key), user, user);
    }

    private void OnCodexAfterInteract(EntityUid uid, HereticCodexComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        var user = args.User;
        if (!TryComp<HereticComponent>(user, out _))
            return;

        if (!comp.IsOpen)
            return;

        if (args.Target is { } target)
        {
            // Click on rune → curse (Morbus) or erase
            if (TryComp<HereticRuneComponent>(target, out var rune))
            {
                args.Handled = true;
                if (comp.CanCurseRunes && !HasComp<HereticCursedRuneComponent>(target))
                {
                    AddComp<HereticCursedRuneComponent>(target);
                    _popup.PopupEntity(Loc.GetString("heretic-codex-morbus-rune-cursed"), user, user, PopupType.Medium);
                }
                else
                {
                    RemComp<HereticCursedRuneComponent>(target);
                    EraseRune(target, rune, user);
                }
                return;
            }

            // Click on rift → absorb with 2× knowledge
            if (TryComp<HereticRealityRiftComponent>(target, out var rift))
            {
                args.Handled = true;
                _popup.PopupEntity(Loc.GetString("heretic-rift-absorbing"), user, user);
                var absorbTime = _heretic.HasMorbiusCodex(user)
                    ? rift.AbsorbTime * 0.5f
                    : rift.AbsorbTime;

                var eyeEffect = Spawn("HereticEyeDrippingEffect", Transform(user).Coordinates);
                _xform.SetParent(eyeEffect, user);
                if (TryComp<TimedDespawnComponent>(eyeEffect, out var timed))
                    timed.Lifetime = absorbTime;

                _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, absorbTime,
                    new AbsorbRiftCodexDoAfterEvent { EffectEntity = GetNetEntity(eyeEffect) },
                    eventTarget: user,
                    used: target)
                {
                    BreakOnMove   = true,
                    BreakOnDamage = true,
                    NeedHand      = false,
                });

                return;
            }

            return;
        }

        // Click on empty tile → fast rune draw
        args.Handled = true;
        StartRuneDraw(user, comp.DrawTimeFast, comp.DrawEffectEntity);
    }

    private void OnAbsorbRiftCodexDoAfter(EntityUid uid, HereticComponent comp, AbsorbRiftCodexDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
        {
            var eff = GetEntity(args.EffectEntity);
            if (!Deleted(eff))
                QueueDel(eff);
            return;
        }
        if (args.Used == null || !TryComp<HereticRealityRiftComponent>(args.Used, out var rift))
            return;

        // Verify user still holds an open codex
        var hasOpenCodex = false;
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (TryComp<HereticCodexComponent>(held, out var codex) && codex.IsOpen)
            {
                hasOpenCodex = true;
                break;
            }
        }
        if (!hasOpenCodex)
            return;

        args.Handled = true;
        var gain = rift.KnowledgeGain * 2;
        _heretic.AddKnowledgePoints(uid, comp, gain);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Magic/Eldritch/voidblink.ogg"), uid);
        _popup.PopupEntity(Loc.GetString("heretic-rift-absorbed-codex", ("kp", gain)), uid, uid, PopupType.Medium);
        _heretic.SendHereticMessage(uid, Loc.GetString("heretic-rift-absorbed-chat"));

        var coords = _xform.GetMapCoordinates(args.Used.Value);
        var delay  = TimeSpan.FromSeconds(rift.RespawnDelay);
        QueueDel(args.Used.Value);
        Timer.Spawn(delay, () => Spawn("HereticRealityBreach", coords));
    }

    private void EraseRune(EntityUid runeUid, HereticRuneComponent rune, EntityUid user)
    {
        if (rune.Caster != user)
        {
            _popup.PopupEntity(Loc.GetString("heretic-rune-not-yours"), user, user);
            return;
        }

        Spawn("HereticEffectRuneEraseCodex", Transform(runeUid).Coordinates);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_singlebeat.ogg"), runeUid);
        _popup.PopupEntity(Loc.GetString("heretic-rune-erased"), user, user, PopupType.Medium);
        QueueDel(runeUid);
    }

    private void StartRuneDraw(EntityUid uid, float drawTime, string? drawEffectEntity = null)
    {
        _popup.PopupEntity(Loc.GetString("heretic-rune-drawing-start"), uid, uid);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, uid, drawTime,
            new DrawHereticRuneDoAfterEvent(),
            eventTarget: uid)
        {
            BreakOnMove   = true,
            BreakOnDamage = true,
        });

        var drawEffect = drawEffectEntity ?? (drawTime < DrawTime ? "HereticEffectRuneDrawFast" : "HereticEffectRuneDraw");
        _drawEffects[uid] = Spawn(drawEffect, Transform(uid).Coordinates);
    }

    private void OnDrawRuneDoAfter(EntityUid uid, HereticComponent comp, DrawHereticRuneDoAfterEvent args)
    {
        if (_drawEffects.Remove(uid, out var effect) && Exists(effect))
            Del(effect);

        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;

        var coords = Transform(uid).Coordinates;
        EnforceRuneLimit(uid);
        var rune = Spawn("HereticTransmutationRune", coords);
        if (TryComp<HereticRuneComponent>(rune, out var runeComp))
            runeComp.Caster = uid;

        _popup.PopupEntity(Loc.GetString("heretic-rune-drawn"), uid, uid, PopupType.Medium);
    }

    // ─── Rune Interaction ─────────────────────────────────────────────────────

    private void OnRuneActivated(EntityUid runeUid, HereticRuneComponent rune, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<HereticComponent>(args.User, out var heretic))
            return;

        // Only the caster (or any heretic, depending on design) can use the rune
        if (rune.Caster != args.User)
        {
            _popup.PopupEntity(Loc.GetString("heretic-rune-not-yours"), args.User, args.User);
            return;
        }

        args.Handled = true;

        var available = GetAvailableRituals(args.User, heretic);

        if (available.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-ritual-none-available"), args.User, args.User);
            return;
        }

        var state = new HereticRitualBuiState
        {
            Rituals = available.Select(p => new HereticRitualNodeData
            {
                Id   = p.ID,
                Name = Loc.GetString(p.Name),
                Icon = _proto.TryIndex<HereticKnowledgePrototype>(p.RequiredKnowledge, out var kp) ? kp.Icon : null,
                Ingredients = p.Ingredients.Select(i => new HereticRitualIngredientData
                {
                    EntityId    = i.EntityId,
                    Tag         = i.Tag,
                    HasMobState = i.HasMobState,
                    IsBurning   = i.IsBurning,
                    Amount      = i.Amount,
                }).ToList(),
            }).ToList(),
            Offerings = new List<HereticOfferingNodeData>(),
        };

        _ui.SetUiState(runeUid, HereticRitualBuiKey.Key, state);
        _ui.TryOpenUi(runeUid, HereticRitualBuiKey.Key, args.User);
    }

    private void OnRitualSelected(EntityUid runeUid, HereticRuneComponent rune, HereticSelectRitualMessage args)
    {
        if (!TryComp<HereticComponent>(args.Actor, out var heretic))
            return;

        if (!_proto.TryIndex<HereticRitualPrototype>(args.RitualId, out var ritual))
            return;

        // Verify actor is the caster
        if (rune.Caster != args.Actor)
            return;

        // Изломанный ритуал: ищем труп с душой, любой костюм, латексные/нитриловые перчатки.
        EntityUid? shatteredCorpse = null;
        if (ritual.ID == "RitualShatteredRitual")
        {
            var coordsCheck = Transform(runeUid).Coordinates;
            EntityUid? shatteredSuit = null;
            EntityUid? shatteredGloves = null;

            foreach (var nearEnt in _lookup.GetEntitiesInRange(coordsCheck, rune.IngredientSearchRadius))
            {
                if (shatteredCorpse == null &&
                    TryComp<MobStateComponent>(nearEnt, out var ms) &&
                    _mobs.IsDead(nearEnt, ms) &&
                    HasComp<HumanoidProfileComponent>(nearEnt) &&
                    _mind.TryGetMind(nearEnt, out _, out _))
                {
                    shatteredCorpse = nearEnt;
                    continue;
                }

                if (shatteredSuit == null &&
                    TryComp<ClothingComponent>(nearEnt, out var cloth) &&
                    cloth.Slots.HasFlag(SlotFlags.OUTERCLOTHING))
                {
                    shatteredSuit = nearEnt;
                    continue;
                }

                if (shatteredGloves == null)
                {
                    var pid = MetaData(nearEnt).EntityPrototype?.ID;
                    if (pid == "ClothingHandsGlovesLatex" || pid == "ClothingHandsGlovesNitrile")
                        shatteredGloves = nearEnt;
                }
            }

            if (shatteredCorpse == null)
            {
                _popup.PopupEntity(Loc.GetString("heretic-shattered-ritual-fail-no-corpse"), args.Actor, args.Actor, PopupType.SmallCaution);
                return;
            }
            if (shatteredSuit == null)
            {
                _popup.PopupEntity(Loc.GetString("heretic-shattered-ritual-fail-no-suit"), args.Actor, args.Actor, PopupType.SmallCaution);
                return;
            }
            if (shatteredGloves == null)
            {
                _popup.PopupEntity(Loc.GetString("heretic-shattered-ritual-fail-no-gloves"), args.Actor, args.Actor, PopupType.SmallCaution);
                return;
            }

            QueueDel(shatteredSuit.Value);
            QueueDel(shatteredGloves.Value);
        }

        // Несовершенный ритуал: труп + мак.
        EntityUid? voicelessCorpse = null;
        EntityUid? voicelessPoppy = null;
        if (ritual.ID == "RitualVoicelessDead")
        {
            var areaCoords = Transform(runeUid).Coordinates;
            foreach (var nearEnt in _lookup.GetEntitiesInRange(areaCoords, rune.IngredientSearchRadius))
            {
                if (voicelessCorpse == null &&
                    TryComp<MobStateComponent>(nearEnt, out var ms) &&
                    _mobs.IsDead(nearEnt, ms))
                {
                    voicelessCorpse = nearEnt;
                    continue;
                }
                if (voicelessPoppy == null &&
                    MetaData(nearEnt).EntityPrototype?.ID == "FoodPoppy")
                    voicelessPoppy = nearEnt;
            }

            if (voicelessCorpse == null)
            {
                _popup.PopupEntity(Loc.GetString("heretic-imperfect-ritual-fail-no-corpse"), args.Actor, args.Actor, PopupType.SmallCaution);
                return;
            }
            if (voicelessPoppy == null)
            {
                _popup.PopupEntity(Loc.GetString("heretic-imperfect-ritual-fail-no-poppies"), args.Actor, args.Actor, PopupType.SmallCaution);
                return;
            }
            if (CountActiveVoicelessDead(args.Actor) >= 2)
            {
                _popup.PopupEntity(Loc.GetString("heretic-imperfect-ritual-limit"), args.Actor, args.Actor, PopupType.Small);
                return;
            }
        }

        // Ритуал Сплетения пустоты требует температуры ниже 0°C
        if (ritual.ID == "RitualVoidWeave")
        {
            var tileMix = _atmosphere.GetTileMixture(runeUid);
            if (tileMix == null || tileMix.Temperature >= Atmospherics.T0C)
            {
                _popup.PopupEntity(Loc.GetString("heretic-void-weave-too-warm"), args.Actor, args.Actor, PopupType.SmallCaution);
                return;
            }
        }

        // Check ingredients within range
        var coords = Transform(runeUid).Coordinates;
        var missingNames = GetMissingIngredientNames(coords, rune.IngredientSearchRadius, ritual, args.Actor);
        if (missingNames.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-ritual-missing-ingredients"), args.Actor, args.Actor);
            if (TryComp<ActorComponent>(args.Actor, out var ritualActor))
            {
                var items = string.Join(", ", missingNames);
                _chatManager.DispatchServerMessage(ritualActor.PlayerSession,
                    Loc.GetString("heretic-ritual-missing-ingredients-list", ("items", items)));
            }
            return;
        }

        TryConsumeIngredients(coords, rune.IngredientSearchRadius, ritual, args.Actor);

        // Sundered blade craft limit (max 5)
        if (ritual.ID == "RitualBladeSundered")
        {
            if (!_heretic.CanCraftSunderedBlade(args.Actor))
            {
                _popup.PopupEntity(Loc.GetString("heretic-sundered-blade-limit"), args.Actor, args.Actor, PopupType.Small);
                return;
            }
            _heretic.IncrementSunderedBladeCraft(args.Actor);
        }

        // Key blade craft limit (max 2)
        if (ritual.ID == "RitualKeyBlade")
        {
            if (!_heretic.CanCraftKeyBlade(args.Actor))
            {
                _popup.PopupEntity(Loc.GetString("heretic-key-blade-limit"), args.Actor, args.Actor, PopupType.Small);
                return;
            }
            _heretic.IncrementKeyBladeCraft(args.Actor);
        }

        // Изломанный ритуал: лимит 1, поднимаем труп как Разбитого восставшего и передаём управление игроку
        if (ritual.ID == "RitualShatteredRitual")
        {
            if (!_heretic.CanCraftShatteredGhoul(args.Actor))
            {
                _popup.PopupEntity(Loc.GetString("heretic-shattered-ghoul-limit"), args.Actor, args.Actor, PopupType.Small);
                return;
            }

            if (shatteredCorpse is { } corpseUid)
            {
                // Пометить как разбитого восставшего
                var ghoulComp = EnsureComp<HereticGhoulComponent>(corpseUid);
                ghoulComp.Master = args.Actor;
                Dirty(corpseUid, ghoulComp);

                // Добавить во фракцию приспешников
                _faction.AddFaction(corpseUid, FamiliarFaction);

                // Установить порог смерти на 125 HP
                _mobThresholds.SetMobStateThreshold(corpseUid, 125, MobState.Dead);

                // Окрасить кожу игрока в тёмно-синий
                if (_visualBody.TryGatherMarkingsData(corpseUid, null, out var profiles, out _, out _))
                {
                    var coloredProfiles = profiles.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value with { SkinColor = Color.FromHex("#00008b") });
                    _visualBody.ApplyProfiles(corpseUid, coloredProfiles);
                }

                // Воскресить и полностью вылечить
                _mobs.ChangeMobState(corpseUid, MobState.Alive);
                _damageable.SetAllDamage(corpseUid, 0);

                // Выбросить все предметы из рук трупа
                if (TryComp<HandsComponent>(corpseUid, out var handsComp))
                {
                    foreach (var hand in handsComp.Hands.Keys.ToList())
                    {
                        _hands.TryDrop((corpseUid, handsComp), hand, checkActionBlocker: false);
                    }
                }

                // Выдать два несъёмных жестоких оружия в руки
                var weaponCoords = Transform(corpseUid).Coordinates;
                for (var i = 0; i < 2; i++)
                {
                    var weapon = Spawn("HereticGhoulBrutalFist", weaponCoords);
                    AddComp<UnremoveableComponent>(weapon);
                    if (!_hands.TryPickupAnyHand(corpseUid, weapon, checkActionBlocker: false))
                        QueueDel(weapon);
                }

                _popup.PopupEntity(Loc.GetString("heretic-shattered-risen-revived"), corpseUid, corpseUid, PopupType.LargeCaution);

                // Уведомить гуля в чате переливающимся фиолетовым
                if (TryComp<ActorComponent>(corpseUid, out var corpseActor))
                {
                    var rawMsg = Loc.GetString("heretic-shattered-risen-chat");
                    var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", rawMsg));
                    _chatManager.ChatMessageToOne(ChatChannel.Server, rawMsg, wrapped, default, false, corpseActor.PlayerSession.Channel);
                }

                _heretic.IncrementShatteredGhoulCraft(args.Actor);
            }
        }

        // Block FeastOfOwls if ascension not denied yet
        if (ritual.ID == "FeastOfOwls" && !heretic.AscensionDenied)
        {
            if (TryComp<ActorComponent>(args.Actor, out var feastActor))
                _chatManager.DispatchServerMessage(feastActor.PlayerSession, Loc.GetString("heretic-feast-of-owls-warning"));
            return;
        }

        // Spawn results
        foreach (var resultProto in ritual.Results)
        {
            var spawned = Spawn(resultProto, coords);
            if (TryComp<HereticFeastOwlsResultComponent>(spawned, out var feast))
                _feastOfOwls.OnFeastCreated(spawned, feast, args.Actor);
            if (TryComp<HereticWarrenKingGreetingResultComponent>(spawned, out var greeting))
                _warrenKing.OnGreetingCreated(spawned, greeting, args.Actor);
            if (TryComp<HereticAshManComponent>(spawned, out var ashMan))
                ashMan.CasterUid = args.Actor;
            if (TryComp<HereticRawProphetComponent>(spawned, out var rawProphet))
                rawProphet.Master = args.Actor;
            if (TryComp<HereticStalkerComponent>(spawned, out var stalker))
                stalker.Master = args.Actor;
            if (TryComp<HereticFireSharkComponent>(spawned, out var fireShark))
            {
                foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, rune.IngredientSearchRadius))
                {
                    if (ent.Owner == args.Actor)
                        continue;
                    fireShark.TargetUid = ent.Owner;
                    break;
                }
            }
            if (_faction.IsMember(spawned, FamiliarFaction))
                _heretic.AddSummon(args.Actor);
            if (TryComp<HereticPactTokenComponent>(spawned, out _) && heretic.PassiveLevel < 3)
            {
                _heretic.SetPassiveLevel(args.Actor, heretic, 3);
                if (heretic.CurrentPath == HereticPath.Ash)
                    _ashPassive.ApplyPassiveLevel3(args.Actor);
                if (heretic.CurrentPath == HereticPath.Flesh)
                    _fleshPassive.ApplyPassiveLevel3(args.Actor);
                if (heretic.CurrentPath == HereticPath.Void)
                    _voidPassive.ApplyPassiveLevel3(args.Actor);
                if (heretic.CurrentPath == HereticPath.Rust)
                    _rustPassive.ApplyPassiveLevel1(args.Actor);
                if (heretic.CurrentPath == HereticPath.Cosmos)
                    _cosmosPassive.ApplyPassiveLevel3(args.Actor);
                _heretic.AddKnowledgePoints(args.Actor, heretic, 4);
                _heretic.SendInfoBuiState(args.Actor, heretic);
                _popup.PopupEntity(Loc.GetString("heretic-pact-ritual-complete"), args.Actor, args.Actor, PopupType.Large);
                _heretic.SendHereticMessage(args.Actor, Loc.GetString("heretic-pact-ritual-complete-chat"));
                QueueDel(spawned);
            }
            if (TryComp<HereticAscensionResultComponent>(spawned, out _))
            {
                _heretic.TryTriggerAscension(args.Actor);
                QueueDel(spawned);
            }
            if (TryComp<HereticPathRobeComponent>(spawned, out _))
                _heretic.ActivateHereticAura(args.Actor, heretic);
        }

        if (ritual.ID == "RitualVoicelessDead")
        {
            if (voicelessCorpse is { } vCorpse && voicelessPoppy is { } vPoppy)
            {
                QueueDel(vPoppy);
                var corpsePos = _xform.GetMapCoordinates(vCorpse);
                QueueDel(vCorpse);
                var dead = Spawn("HereticVoicelessDead", corpsePos);
                if (TryComp<HereticVoicelessDeadComponent>(dead, out var deadComp))
                    deadComp.Master = args.Actor;
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_curse.ogg"), args.Actor);
                _popup.PopupEntity(Loc.GetString("heretic-imperfect-ritual-success"), args.Actor, args.Actor, PopupType.Large);
            }
        }

        if (ritual.ID == "RitualHeartbeatMansus")
        {
            // Ищем именованных целей и культистов крови рядом с руной: крит/мёртвые — немедленный гиб, спящие — с задержкой
            var toSacrifice = new List<(EntityUid Uid, bool IsImmediate, bool IsNamedTarget)>();
            var toSacrificeUids = new HashSet<EntityUid>();
            foreach (var nearEnt in _lookup.GetEntitiesInRange(coords, rune.IngredientSearchRadius))
            {
                var isNamed = _heretic.IsNamedTarget(heretic, nearEnt);
                var isCultist = HasComp<CultistComponent>(nearEnt);
                if (!isNamed && !isCultist) continue;
                if (!toSacrificeUids.Add(nearEnt)) continue;
                if (_mobs.IsCritical(nearEnt) || _mobs.IsDead(nearEnt))
                    toSacrifice.Add((nearEnt, true, isNamed));
                else if (HasComp<SleepingComponent>(nearEnt))
                    toSacrifice.Add((nearEnt, false, isNamed));
            }

            if (toSacrifice.Count > 0)
            {
                var hereticUid = args.Actor;
                foreach (var (target, isImmediate, isNamedTarget) in toSacrifice)
                {
                    var points = isNamedTarget ? 2 : 1;
                    if (isImmediate)
                    {
                        var isHead = HasComp<CommandStaffComponent>(target);
                        _gibbing.Gib(target);
                        if (TryComp<HereticComponent>(hereticUid, out var h))
                        {
                            if (isNamedTarget)
                                _heretic.RemoveNamedTarget(hereticUid, h, target);
                            _heretic.AddKnowledgePoints(hereticUid, h, points);
                        }
                        if (isHead)
                            _heretic.AddHighValueSacrifice(hereticUid);
                        else
                            _heretic.AddSacrifice(hereticUid);
                    }
                    else
                    {
                        // Спящая цель: трясётся 5 секунд, затем гибается
                        var isHead = HasComp<CommandStaffComponent>(target);
                        _jitter.AddJitter(target, 20f, 6f);
                        Timer.Spawn(5000, () =>
                        {
                            if (!Exists(target)) return;
                            _gibbing.Gib(target);
                            if (!Exists(hereticUid) || !TryComp<HereticComponent>(hereticUid, out var h2)) return;
                            if (isNamedTarget)
                                _heretic.RemoveNamedTarget(hereticUid, h2, target);
                            _heretic.AddKnowledgePoints(hereticUid, h2, points);
                            if (isHead)
                                _heretic.AddHighValueSacrifice(hereticUid);
                            else
                                _heretic.AddSacrifice(hereticUid);
                        });
                    }
                }
                _popup.PopupEntity(Loc.GetString("heretic-ritual-heartbeat-sacrifice"), args.Actor, args.Actor, PopupType.Large);
                if (TryComp<ActorComponent>(args.Actor, out var sacrificeActor))
                {
                    var sacrificeMsg = Loc.GetString("heretic-sacrifice-chat");
                    var sacrificeWrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", sacrificeMsg));
                    _chatManager.ChatMessageToOne(ChatChannel.Server, sacrificeMsg, sacrificeWrapped, default, false, sacrificeActor.PlayerSession.Channel);
                }
            }
            else if (heretic.NamedTargets.Count > 0)
            {
                // Цели уже назначены, но рядом никого жертвовать нельзя
                _popup.PopupEntity(Loc.GetString("heretic-ritual-heartbeat-already-has-targets"), args.Actor, args.Actor, PopupType.SmallCaution);
                return;
            }
            else
            {
                // Целей нет — назначаем новые: 1 глава (CommandStaff) + 4 обычных
                EntityUid? headTarget = null;
                var regularTargets = new List<EntityUid>();
                var mobQuery = EntityQueryEnumerator<MobStateComponent, HumanoidProfileComponent>();
                while (mobQuery.MoveNext(out var mobUid, out _, out _))
                {
                    if (mobUid == args.Actor) continue;
                    if (HasComp<CommandStaffComponent>(mobUid) && headTarget == null)
                        headTarget = mobUid;
                    else
                        regularTargets.Add(mobUid);
                }

                var targets = new List<EntityUid>();
                if (headTarget.HasValue)
                    targets.Add(headTarget.Value);
                foreach (var t in regularTargets)
                {
                    if (targets.Count >= 5) break;
                    targets.Add(t);
                }

                if (targets.Count == 0)
                {
                    _popup.PopupEntity(Loc.GetString("heretic-ritual-heartbeat-no-targets"), args.Actor, args.Actor, PopupType.SmallCaution);
                    return;
                }

                _heretic.SetNamedTargets(args.Actor, heretic, targets);
                var targetNames = string.Join(", ", targets.Select(t => MetaData(t).EntityName));
                _popup.PopupEntity(Loc.GetString("heretic-named-targets-assigned", ("names", targetNames)), args.Actor, args.Actor, PopupType.LargeCaution);
            }
        }

        if (ritual.ID == "RitualRelentlessHeartbeat")
        {
            if (_mind.TryGetMind(args.Actor, out var mindId, out _))
                _heretic.AssignNamedTargets(args.Actor, mindId);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_effects_singlebeat.ogg"), args.Actor);
            _popup.PopupEntity(Loc.GetString("heretic-relentless-heartbeat"), args.Actor, args.Actor, PopupType.Large);
        }

        Spawn("HereticEffectRuneActivate", coords);
        Spawn("HereticEffectRingleader", coords);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/heretic/sound_magic_castsummon.ogg"), args.Actor);

        _popup.PopupEntity(Loc.GetString("heretic-ritual-success", ("name", Loc.GetString(ritual.Name))), args.Actor, args.Actor, PopupType.Large);
    }

    private void OnRuneBuiRangeCheck(EntityUid uid, HereticRuneComponent comp, BoundUserInterfaceCheckRangeEvent args)
    {
        args.Result = BoundUserInterfaceRangeResult.Pass;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private List<HereticRitualPrototype> GetAvailableRituals(EntityUid caster, HereticComponent heretic)
    {
        var result = new List<HereticRitualPrototype>();
        foreach (var proto in _proto.EnumeratePrototypes<HereticRitualPrototype>())
        {
            if (!_heretic.HasKnowledge(heretic, proto.RequiredKnowledge))
                continue;
            if (proto.ID == "RitualBladeSundered" && !_heretic.CanCraftSunderedBlade(caster))
                continue;
            if (proto.ID == "RitualKeyBlade" && !_heretic.CanCraftKeyBlade(caster))
                continue;
            result.Add(proto);
        }
        return result;
    }

    private List<string> GetMissingIngredientNames(EntityCoordinates center, float radius, HereticRitualPrototype ritual, EntityUid actor = default)
    {
        var nearby  = _lookup.GetEntitiesInRange(center, radius);
        var claimed = new HashSet<EntityUid>();
        var missing = new List<string>();

        foreach (var ing in ritual.Ingredients)
        {
            var needed = ing.Amount;
            foreach (var nearEnt in nearby)
            {
                if (needed <= 0)
                    break;
                if (claimed.Contains(nearEnt) || nearEnt == actor)
                    continue;

                bool matches;
                if (ing.Tag is { } tag)
                    matches = _tag.HasTag(nearEnt, tag);
                else if (ing.IsBurning)
                    matches = HasComp<MobStateComponent>(nearEnt) && TryComp<FlammableComponent>(nearEnt, out var fl) && fl.OnFire;
                else if (ing.HasMobState)
                    matches = HasComp<MobStateComponent>(nearEnt);
                else
                    matches = MetaData(nearEnt).EntityPrototype?.ID == (string)ing.EntityId;

                if (!matches)
                    continue;

                var available = TryComp<StackComponent>(nearEnt, out var stack) ? stack.Count : 1;
                claimed.Add(nearEnt);
                needed -= Math.Min(available, needed);
            }

            if (needed > 0)
            {
                string name;
                if (ing.HasMobState)
                    name = Loc.GetString("heretic-ritual-ingredient-mob");
                else if (ing.Tag is { } tag)
                {
                    var locKey = $"heretic-ritual-ingredient-tag-{tag}";
                    var loc = Loc.GetString(locKey);
                    name = loc == locKey ? tag : loc;
                }
                else
                    name = _proto.TryIndex<EntityPrototype>((string)ing.EntityId, out var ep) ? ep.Name : (string)ing.EntityId;

                missing.Add($"{needed}× {name}");
            }
        }

        return missing;
    }

    private bool TryConsumeIngredients(EntityCoordinates center, float radius, HereticRitualPrototype ritual, EntityUid actor = default)
    {
        var nearby = _lookup.GetEntitiesInRange(center, radius);

        var claimed = new HashSet<EntityUid>();
        var consumePlan = new List<(EntityUid Entity, int Amount)>();
        foreach (var ing in ritual.Ingredients)
        {
            var needed = ing.Amount;
            foreach (var nearEnt in nearby)
            {
                if (needed <= 0)
                    break;

                if (claimed.Contains(nearEnt))
                    continue;

                if (nearEnt == actor)
                    continue;

                bool matches;
                if (ing.Tag is { } tag)
                    matches = _tag.HasTag(nearEnt, tag);
                else if (ing.IsBurning)
                    matches = HasComp<MobStateComponent>(nearEnt) && TryComp<FlammableComponent>(nearEnt, out var fl) && fl.OnFire;
                else if (ing.HasMobState)
                    matches = HasComp<MobStateComponent>(nearEnt);
                else
                    matches = MetaData(nearEnt).EntityPrototype?.ID == (string)ing.EntityId;

                if (!matches)
                    continue;

                var available = TryComp<StackComponent>(nearEnt, out var stack) ? stack.Count : 1;
                var take = Math.Min(available, needed);

                claimed.Add(nearEnt);
                consumePlan.Add((nearEnt, take));
                needed -= take;
            }

            if (needed > 0)
                return false;
        }

        // All found — consume
        foreach (var (ent, amount) in consumePlan)
        {
            if (TryComp<StackComponent>(ent, out var stackComp))
                _stack.ReduceCount((ent, stackComp), amount);
            else
                QueueDel(ent);
        }

        return true;
    }

    private void EnforceRuneLimit(EntityUid caster, int maxRunes = 3)
    {
        var runes = new List<EntityUid>();
        var query = EntityQueryEnumerator<HereticRuneComponent>();
        while (query.MoveNext(out var uid, out var runeComp))
        {
            if (runeComp.Caster == caster)
                runes.Add(uid);
        }

        while (runes.Count >= maxRunes)
        {
            QueueDel(runes[0]);
            runes.RemoveAt(0);
        }
    }

    private int CountActiveVoicelessDead(EntityUid master)
    {
        var count = 0;
        var query = EntityQueryEnumerator<HereticVoicelessDeadComponent, MobStateComponent>();
        while (query.MoveNext(out _, out var vd, out var mob))
        {
            if (vd.Master == master && mob.CurrentState == MobState.Alive)
                count++;
        }
        return count;
    }
}
