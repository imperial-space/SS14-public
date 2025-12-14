using Content.Server.Cult.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Server.Player;
using System.Linq;
using Content.Shared.Popups;
using Content.Server.Body.Components;
using Robust.Shared.Timing;
using Content.Server.SpikeTrap.Components;
using Content.Server.MagicBarrier.Components;
using Content.Shared.Speech;
using Content.Server.Chat.Systems;
using Robust.Shared.Spawners;
using Content.Shared.Damage;
using Content.Shared.Nocturn.Components;
using Content.Server.Administration.Systems;
using Content.Shared.Imperial.Medieval.Magic.Mana;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Content.Shared.Random.Helpers;
using Content.Server.Administration;
using Content.Shared.Alert;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Server.SSDFree;
using Content.Server.SSDFree.Components;
using Content.Shared.SSDFree.Components;
using Content.Shared.Cuffs.Components;
using Robust.Shared.Containers;
using Content.Shared.Containers;
using Content.Shared.Body.Components;

namespace Content.Server.Cult
{
    public sealed partial class MedievalMeleeResourceSystem : EntitySystem
    {
        [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly ChatSystem _chat = default!;
        [Dependency] private readonly RejuvenateSystem _rejuv = default!;
        [Dependency] private readonly DamageableSystem _damageableSystem = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
        [Dependency] private readonly MetaDataSystem _metaData = default!;
        [Dependency] private readonly AlertsSystem _alertsSystem = default!;
        [Dependency] private readonly InventorySystem _inventorySystem = default!;
        [Dependency] private readonly SSDFreeSystem _ssdFreeSystem = default!;
        [Dependency] private readonly SharedContainerSystem _container = default!;

        private const float DefaultReloadTimeSeconds = 10f;

        private TimeSpan _nextCheckTime;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<CultBrushComponent, BeforeRangedInteractEvent>(OnUseBrushInHand);
            SubscribeLocalEvent<CultCrystallComponent, BeforeRangedInteractEvent>(OnUseCrystallInHand);
            SubscribeLocalEvent<CultCheckPictureComponent, ActivateInWorldEvent>(OnActivated);
            SubscribeLocalEvent<CultCheckPictureComponent, ExaminedEvent>(OnExamine);
            SubscribeLocalEvent<CultTeleportComponent, ExaminedEvent>(OnExamineTp);
            SubscribeLocalEvent<CultCursedComponent, ExaminedEvent>(OnExamineCursed);
            SubscribeLocalEvent<CultMemberComponent, MoveEvent>(OnChangeParent);
            SubscribeLocalEvent<CultRitualMeleeComponent, MeleeHitEvent>(OnMeleeHit);
            SubscribeLocalEvent<CultBloodMeleeComponent, MeleeHitEvent>(OnBloodMeleeHit);
            SubscribeLocalEvent<TakeNameComponent, PlayerAttachedEvent>(OnPlayerAttached);

            _nextCheckTime = _timing.CurTime + TimeSpan.FromSeconds(DefaultReloadTimeSeconds);
        }
        private bool CheckCultWearing(EntityUid uid)
        {
            if (!HasComp<CultMemberComponent>(uid) || !TryComp<InventoryComponent>(uid, out var inventoryComponent)) return false;
            var check1 = _inventorySystem.TryGetSlotEntity(uid, "outerClothing", out var slot1, inventoryComponent);
            var check2 = _inventorySystem.TryGetSlotEntity(uid, "head", out var slot2, inventoryComponent);
            if (!check1 || !check2 || !HasComp<CultClothingComponent>(slot1) || !HasComp<CultClothingComponent>(slot2)) return false;
            return true;
        }
        private void OnPlayerAttached(EntityUid uid, TakeNameComponent comp, PlayerAttachedEvent args)
        {
            if (!_playerManager.TryGetSessionByEntity(uid, out var session) || comp.HasName) return;
            _quickDialog.OpenDialog(session, "Введите имя", "Имя", (string message) =>
            {
                _metaData.SetEntityName(uid, message);
                comp.HasName = true;
            });
        }
        private void OnBloodMeleeHit(EntityUid uid, CultBloodMeleeComponent component, MeleeHitEvent args)
        {
            foreach (var entity in args.HitEntities)
            {
                if (!TryComp<CultCursedComponent>(args.User, out var cursed)) continue;
                if (entity != args.User) continue;
                if (cursed.CurseLevel > 75)
                {
                    _chat.TrySendInGameICMessage(cursed.Owner, "Кровь культу была отдана совсем недавно, больше ее пока не нужно, надо подождать", InGameICChatType.Whisper, false);
                    return;
                }
                var xform = Transform(entity);
                var coords = xform.Coordinates;
                foreach (var target in _lookup.GetEntitiesInRange(coords, 2.5f))
                {
                    if (TryComp<CultTeleportComponent>(target, out var tp) && !tp.Base)
                    {

                        _chat.TrySendInGameICMessage(cursed.Owner, "Ave truth...", InGameICChatType.Whisper, false);
                        cursed.CurseLevel = cursed.MaxCurseLevel;
                        foreach (var altar in EntityManager.EntityQuery<CultAltarComponent>())
                        {
                            var axform = Transform(altar.Owner);
                            var acoords = axform.Coordinates;
                            Spawn("MedievalCultCrystallRed", acoords);
                            Spawn("MedievalCultCrystallRed", acoords);
                            Spawn("MedievalCultCrystallRed", acoords);
                        }
                    }
                }
            }
        }

        private void OnMeleeHit(EntityUid uid, CultRitualMeleeComponent component, MeleeHitEvent args)
        {
            if (!HasComp<CultMemberComponent>(args.User)) return;
            foreach (var entity in args.HitEntities)
            {
                if (entity != args.User) continue;
                var from = GetTeleport(args.User);
                if (!TryComp<CultTeleportComponent>(from, out var teleport) || !teleport.Enabled) continue;
                if (from != args.User)
                {
                    if (!CheckCultWearing(args.User))
                    {
                        _chat.TrySendInGameICMessage(args.User, "Нужны святые одеяния...", InGameICChatType.Whisper, false);
                        return;
                    }
                    var xform = Transform(from);
                    var coords = xform.Coordinates;
                    foreach (var target in _lookup.GetEntitiesInRange(coords, 2.5f))
                    {
                        foreach (var tp in EntityManager.EntityQuery<CultTeleportComponent>())
                        {
                            if (tp.Base != teleport.Base && tp.Sector == teleport.Sector && HasComp<MedievalSpikeTargetComponent>(target))
                            {
                                var teleported = EnsureComp<CultTeleportedComponent>(target);
                                teleported.Portal = from;
                                var txform = Transform(target);
                                var tcoords = txform.Coordinates;
                                Spawn("MedievalTeleportEffect", tcoords);
                                var newxform = Transform(tp.Owner);
                                var newcoords = newxform.Coordinates;
                                _transform.SetCoordinates(target, newcoords);
                            }
                        }
                    }
                }
            }
        }

        public EntityUid GetTeleport(EntityUid uid)
        {
            var xform = Transform(uid);
            var coords = xform.Coordinates;
            foreach (var target in _lookup.GetEntitiesInRange(coords, 2.5f))
            {
                if (HasComp<CultTeleportComponent>(target))
                {
                    return target;
                }
            }
            return uid;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var curTime = _timing.CurTime;

            if (curTime > _nextCheckTime)
            {
                _nextCheckTime = curTime + TimeSpan.FromSeconds(DefaultReloadTimeSeconds);

                foreach (var heal in EntityManager.EntityQuery<HealCurseComponent>())
                {
                    _damageableSystem.TryChangeDamage(heal.Owner, -heal.RegenDamage, true, false);
                }

                foreach (var cursed in EntityManager.EntityQuery<CultCursedComponent>())
                {
                    cursed.CurseLevel -= cursed.Rate;
                    if (cursed.CurseLevel < 0f)
                    {
                        cursed.CurseLevel = 0f;
                        _alertsSystem.ClearAlert(cursed.Owner, cursed.CurseAlert);
                    }
                    else
                    {
                        _alertsSystem.ShowAlert(cursed.Owner, cursed.CurseAlert, (short)Math.Clamp(Math.Round(cursed.CurseLevel / cursed.MaxCurseLevel * 5.1f), 0, 5));
                    }
                    if (cursed.CurseLevel > 5f && cursed.CurseLevel < 24f)
                    {
                        _damageableSystem.TryChangeDamage(cursed.Owner, cursed.LostDamage, true, false);
                        _popupSystem.PopupEntity("Все ваше тело болит из-за того, что вы не поддерживаете зов культа. Терпеть?", cursed.Owner, cursed.Owner, PopupType.SmallCaution);
                    }
                    if (cursed.CurseLevel > 0f && cursed.CurseLevel <= 5f)
                    {
                        _damageableSystem.TryChangeDamage(cursed.Owner, cursed.LostDamage, true, false);
                        _popupSystem.PopupEntity("Еще немного, и связь с культом разорвется. Терпеть осталось недолго.", cursed.Owner, cursed.Owner, PopupType.SmallCaution);
                    }
                    if (cursed.CurseLevel > 60f && TryComp<DamageableComponent>(cursed.Owner, out var damage) && damage.TotalDamage < 100 && damage.TotalDamage > 5)
                    {
                        _damageableSystem.TryChangeDamage(cursed.Owner, -cursed.RegenDamage, true, false);
                        _popupSystem.PopupEntity("Связь с культом восстанавливает твои раны", cursed.Owner, cursed.Owner, PopupType.Small);
                    }
                }

                foreach (var picture in EntityManager.EntityQuery<CultCheckPictureComponent>())
                {
                    if (picture.CollegiumUnlocked) continue;
                    foreach (var cultist in EntityManager.EntityQuery<CultMemberComponent>())
                    {
                        if (TryComp<CultMapBlockerComponent>(cultist.parent, out var blocker))
                        {
                            switch (blocker.Sector)
                            {
                                case "sector1":
                                    if (!picture.Sector1)
                                    {
                                        _popupSystem.PopupEntity("Этот сектор еще не был разблокирован ритуалом, срочно назад!", cultist.Owner, cultist.Owner, PopupType.LargeCaution);
                                        _audioSystem.PlayEntity("/Audio/Imperial/Medieval/Cult/cult_zone_damage.ogg", Filter.Entities(cultist.Owner), cultist.Owner, false, AudioParams.Default.WithVolume(10f));
                                        _damageableSystem.TryChangeDamage(cultist.Owner, cultist.Damage, true, false);
                                    }
                                    break;
                                case "sector2":
                                    if (!picture.Sector2)
                                    {
                                        _popupSystem.PopupEntity("Этот сектор еще не был разблокирован ритуалом, срочно назад!", cultist.Owner, cultist.Owner, PopupType.LargeCaution);
                                        _audioSystem.PlayEntity("/Audio/Imperial/Medieval/Cult/cult_zone_damage.ogg", Filter.Entities(cultist.Owner), cultist.Owner, false, AudioParams.Default.WithVolume(10f));

                                        _damageableSystem.TryChangeDamage(cultist.Owner, cultist.Damage, true, false);
                                    }
                                    break;
                                case "sector3":
                                    if (!picture.Sector3)
                                    {
                                        _popupSystem.PopupEntity("Этот сектор еще не был разблокирован ритуалом, срочно назад!", cultist.Owner, cultist.Owner, PopupType.LargeCaution);
                                        _audioSystem.PlayEntity("/Audio/Imperial/Medieval/Cult/cult_zone_damage.ogg", Filter.Entities(cultist.Owner), cultist.Owner, false, AudioParams.Default.WithVolume(10f));
                                        _damageableSystem.TryChangeDamage(cultist.Owner, cultist.Damage, true, false);
                                    }
                                    break;
                                case "sector5":
                                    if (!picture.CollegiumUnlocked)
                                    {
                                        _popupSystem.PopupEntity("Для разблокировки сектора с коллегией требуется особый ритуал, срочно назад!", cultist.Owner, cultist.Owner, PopupType.LargeCaution);
                                        _audioSystem.PlayEntity("/Audio/Imperial/Medieval/Cult/cult_zone_damage.ogg", Filter.Entities(cultist.Owner), cultist.Owner, false, AudioParams.Default.WithVolume(10f));
                                        _damageableSystem.TryChangeDamage(cultist.Owner, cultist.Damage, true, false);
                                    }
                                    break;
                                case "sector6":
                                    if (!picture.Sector6)
                                    {
                                        _popupSystem.PopupEntity("Этот сектор еще не был разблокирован ритуалом, срочно назад!", cultist.Owner, cultist.Owner, PopupType.LargeCaution);
                                        _audioSystem.PlayEntity("/Audio/Imperial/Medieval/Cult/cult_zone_damage.ogg", Filter.Entities(cultist.Owner), cultist.Owner, false, AudioParams.Default.WithVolume(10f));
                                        _damageableSystem.TryChangeDamage(cultist.Owner, cultist.Damage, true, false);
                                    }
                                    break;
                                case "sector7":
                                    if (!picture.Sector7)
                                    {
                                        _popupSystem.PopupEntity("Этот сектор еще не был разблокирован ритуалом, срочно назад!", cultist.Owner, cultist.Owner, PopupType.LargeCaution);
                                        _audioSystem.PlayEntity("/Audio/Imperial/Medieval/Cult/cult_zone_damage.ogg", Filter.Entities(cultist.Owner), cultist.Owner, false, AudioParams.Default.WithVolume(10f));
                                        _damageableSystem.TryChangeDamage(cultist.Owner, cultist.Damage, true, false);
                                    }
                                    break;
                                case "sector8":
                                    if (!picture.Sector8)
                                    {
                                        _popupSystem.PopupEntity("Этот сектор еще не был разблокирован ритуалом, срочно назад!", cultist.Owner, cultist.Owner, PopupType.LargeCaution);
                                        _audioSystem.PlayEntity("/Audio/Imperial/Medieval/Cult/cult_zone_damage.ogg", Filter.Entities(cultist.Owner), cultist.Owner, false, AudioParams.Default.WithVolume(10f));
                                        _damageableSystem.TryChangeDamage(cultist.Owner, cultist.Damage, true, false);
                                    }
                                    break;
                                case "sector9":
                                    if (!picture.Sector9)
                                    {
                                        _popupSystem.PopupEntity("Этот сектор еще не был разблокирован ритуалом, срочно назад!", cultist.Owner, cultist.Owner, PopupType.LargeCaution);
                                        _audioSystem.PlayEntity("/Audio/Imperial/Medieval/Cult/cult_zone_damage.ogg", Filter.Entities(cultist.Owner), cultist.Owner, false, AudioParams.Default.WithVolume(10f));
                                        _damageableSystem.TryChangeDamage(cultist.Owner, cultist.Damage, true, false);
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
        }

        private void OnChangeParent(EntityUid uid, CultMemberComponent comp, ref MoveEvent args)
        {
            var newParent = args.NewPosition.EntityId;

            if (!args.ParentChanged)
                return;

            comp.parent = newParent;
        }
        public void OnActivated(EntityUid uid, CultCheckPictureComponent comp, ActivateInWorldEvent args)
        {
            var xform = Transform(uid);
            var coords = xform.Coordinates;
            string figure = GetRuneFigure();

            if (!TryComp<CultMemberComponent>(args.User, out var cultistritualist)) return;

            EnsureComp<SpeechComponent>(uid);
            if (!CheckCultWearing(args.User))
            {
                _chat.TrySendInGameICMessage(uid, "Нужны святые одеяния... без них не провести ритуал", InGameICChatType.Whisper, false);
                foreach (var rune in EntityManager.EntityQuery<CultBloodPaintComponent>())
                {
                    if (rune.Bloody)
                    {
                        EnsureComp<TimedDespawnComponent>(rune.Owner, out var desp);
                        desp.Lifetime = 0.01f;
                    }
                }
                return;
            }

            switch (figure)
            {
                case "christ":
                    if (IsCultistsEnough(uid, 2))
                    {
                        foreach (var center in EntityManager.EntityQuery<CultRitualCenterComponent>())
                        {
                            var isDead = false;
                            var victim = GetVictim(center.Owner);
                            if (TryComp<MobThresholdsComponent>(victim, out var thresholdsComponent) && thresholdsComponent.CurrentThresholdState == MobState.Dead) isDead = true;
                            if (victim != center.Owner)
                            {
                                if (TryComp<BloodstreamComponent>(victim, out var blood))
                                {
                                    //if (blood.BleedAmount > 0)
                                    //{
                                    _chat.TrySendInGameICMessage(uid, "Ритуал проведен успешно, связь цели с культом установлена. Если она будет постоянно жертвовать свою кровь около проклятых сосудов, это принесет культу проклятые криссталы, а жертве - длительную регенерацию", InGameICChatType.Speak, false);
                                    foreach (var altar in EntityManager.EntityQuery<CultAltarComponent>())
                                    {
                                        var axform = Transform(altar.Owner);
                                        var acoords = axform.Coordinates;
                                        Spawn("MedievalCultCrystallRed", acoords);
                                        //if (!isDead) Spawn("MedievalCultCrystallRed", acoords);
                                        if (isDead && TryComp<SSDFreeComponent>(victim, out var ssdfreeComp) && _playerManager.TryGetSessionByEntity(victim, out var session)) _ssdFreeSystem.GoToSSD(victim, session.UserId, false, ssdfreeComp);
                                    }
                                    _audioSystem.PlayPvs(comp.SuccesSound, uid);

                                    var altars = EntityManager.EntityQuery<CultTeleportComponent>();
                                    var needAltars = new List<CultTeleportComponent>();
                                    foreach (var altar in altars)
                                    {
                                        if (!altar.Base)
                                        {
                                            needAltars.Add(altar);
                                        }
                                    }
                                    // var ouraltar = _random.Pick(needAltars); // Obsolete

                                    var ouraltar = EnsureComp<CultTeleportedComponent>(victim).Portal;

                                    var oxform = Transform(ouraltar);
                                    var ocoords = oxform.Coordinates;
                                    _transform.SetCoordinates(victim, ocoords);
                                    if (TryComp<CuffableComponent>(victim, out var cuff))
                                        _container.EmptyContainer(cuff.Container, true);
                                    _audioSystem.PlayEntity(comp.VictimSuccessSound, Filter.Entities(victim), victim, false, AudioParams.Default.WithVolume(20f));
                                    _chat.TrySendInGameICMessage(victim, "Культ истины провел со мной ритуал связи. Если я буду жертвовать кровь... то есть резать себя около этих кровавых сосудов, к одному из которых меня телепортировало, раз в какое-то время, то я буду получать длительную магическую регенерацию, а культ - алые кристаллы. Это... взаимовыгодно? Лишь бы другие не узнали...", InGameICChatType.Whisper, false);
                                    var cyr = EnsureComp<CultCursedComponent>(victim);
                                    cyr.CurseLevel = cyr.MaxCurseLevel;
                                    //}
                                    //else
                                    //{
                                    //    _chat.TrySendInGameICMessage(uid, "На цели ритуала должен быть разрез", InGameICChatType.Speak, false);
                                    //    _audioSystem.PlayPvs(comp.FailSound, uid);
                                    //}
                                }
                            }
                            else
                            {
                                _chat.TrySendInGameICMessage(uid, "Для ритуала связи необходима цель не-культист", InGameICChatType.Speak, false);
                                _audioSystem.PlayPvs(comp.FailSound, uid);
                            }
                        }
                    }
                    break;
                case "axe":
                    if (IsCultistsEnough(uid, 2) && CheckCrystals(uid, comp, 0, 2))
                    {
                        Spawn("MedievalSpawnCultMelee", coords);
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Ритуал призыва оружия проведен успешно", InGameICChatType.Speak, false);
                    }
                    break;
                case "boat":
                    if (IsCultistsEnough(uid, 5) && CheckCrystals(uid, comp, 2, 2))
                    {
                        foreach (var barrier in EntityManager.EntityQuery<MagicBarrierComponent>())
                        {
                            barrier.Stability *= 0.7f;
                        }
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Ритуал повреждения барьера выполнен успешно, его стабильность снижена на треть от текущей", InGameICChatType.Speak, false);
                    }
                    break;
                case "key":
                    if (IsCultistsEnough(uid, 5))
                    {
                        foreach (var picture in EntityManager.EntityQuery<CultCheckPictureComponent>())
                        {
                            if (picture.Sector1 && picture.Sector2 && picture.Sector3 && picture.Sector6 && picture.Sector7 && picture.Sector8 && picture.Sector9)
                            {
                                if (CheckCrystals(uid, comp, 4, 0))
                                {
                                    foreach (var tp in EntityManager.EntityQuery<CultTeleportComponent>())
                                    {
                                        if (tp.Sector == 5)
                                            tp.Enabled = true;
                                    }
                                    comp.CollegiumUnlocked = true;
                                    Spawn("ShockWaveEffect", coords);
                                    _audioSystem.PlayPvs(comp.SuccesSound, uid);
                                    _chat.TrySendInGameICMessage(uid, "Ритуал открытия центральной части острова с коллегией выполнен успешно, смерть грядет!!", InGameICChatType.Speak, false);
                                    _chat.DispatchGlobalAnnouncement("Целостность барьера повреждена, культисты смогли обойти защиту", playSound: true, colorOverride: Color.DeepPink, sender: "Барьер");
                                }
                            }
                            else
                            {
                                _audioSystem.PlayPvs(comp.FailSound, uid);
                                _chat.TrySendInGameICMessage(uid, "Для ритуала открытия центральной части острова с коллегией необходимо разблокировать все остальные сектора", InGameICChatType.Speak, false);

                            }
                        }

                    }
                    break;
                case "swordshield":
                    if (IsCultistsEnough(uid, 3) && CheckCrystals(uid, comp, 1, 2))
                    {
                        Spawn("MedievalClothingOuterArmorCultUp", coords);
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Ритуал призыва защитной робы выполнен успешно", InGameICChatType.Speak, false);
                    }
                    break;
                case "wizard":
                    if (IsCultistsEnough(uid, 3) && CheckCrystals(uid, comp, 1, 2))
                    {
                        Spawn("MedievalClothingOuterArmorCultMana", coords);
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Ритуал призыва магической робы выполнен успешно", InGameICChatType.Speak, false);
                    }
                    break;
                case "heart":
                    if (IsCultistsEnough(uid, 3) && CheckCrystals(uid, comp, 0, 2))
                    {
                        Spawn("CompactDefibrillator", coords);
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Ритуал призыва камня возрождения выполнен успешно", InGameICChatType.Speak, false);
                    }
                    break;
                case "scroll":
                    if (IsCultistsEnough(uid, 3))
                    {
                        if (comp.CollegiumUnlocked)
                        {
                            if (CheckCrystals(uid, comp, 1, 0))
                            {
                                Spawn("MedievalScrollBarrierBad", coords);
                                Spawn("ShockWaveEffect", coords);
                                _audioSystem.PlayPvs(comp.SuccesSound, uid);
                                _chat.TrySendInGameICMessage(uid, "Ритуал призыва проклятого свитка выполнен успешно, отнесите же его к барьеру!", InGameICChatType.Speak, false);
                            }
                        }
                        else
                        {
                            _audioSystem.PlayPvs(comp.FailSound, uid);
                            _chat.TrySendInGameICMessage(uid, "Для призыва проклятых свитков необходимо вначале разблокировать портал в центральную часть острова с коллегией магов.", InGameICChatType.Speak, false);
                        }
                    }
                    break;
                case "wand":
                    if (IsCultistsEnough(uid, 3) && CheckCrystals(uid, comp, 1, 4))
                    {
                        Spawn("MedievalSpellBookRecodeNecro", coords);
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Ритуал призыва магического гримуара выполнен успешно", InGameICChatType.Speak, false);
                    }
                    break;
                case "sector1":
                    if (comp.Sector1)
                    {
                        _audioSystem.PlayPvs(comp.FailSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Этот сектор уже разблокирован", InGameICChatType.Speak, false);
                        break;
                    }
                    if (IsCultistsEnough(uid, 3) && CheckCrystals(uid, comp, comp.NewSectorCost, 0))
                    {
                        comp.Sector1 = true;
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Ритуал открытия нового сектора острова выполнен успешно", InGameICChatType.Speak, false);
                        foreach (var tp in EntityManager.EntityQuery<CultTeleportComponent>())
                        {
                            if (tp.Sector == 1)
                                tp.Enabled = true;
                        }
                        comp.UnlockedSectors++;
                        switch (comp.UnlockedSectors)
                        {
                            case 1: comp.NewSectorCost = 0; break;
                            case 2: comp.NewSectorCost = 1; break;
                            case 3: comp.NewSectorCost = 2; break;
                            case 4: comp.NewSectorCost = 2; break;
                            case 5: comp.NewSectorCost = 3; break;
                            case 6: comp.NewSectorCost = 3; break;
                        }

                    }
                    break;
                case "sector2":
                    if (comp.Sector2)
                    {
                        _audioSystem.PlayPvs(comp.FailSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Этот сектор уже разблокирован", InGameICChatType.Speak, false);
                        break;
                    }
                    if (IsCultistsEnough(uid, 3) && CheckCrystals(uid, comp, comp.NewSectorCost, 0))
                    {
                        comp.Sector2 = true;
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Ритуал открытия нового сектора острова выполнен успешно", InGameICChatType.Speak, false);
                        foreach (var tp in EntityManager.EntityQuery<CultTeleportComponent>())
                        {
                            if (tp.Sector == 2)
                                tp.Enabled = true;
                        }
                        comp.UnlockedSectors++;
                        switch (comp.UnlockedSectors)
                        {
                            case 1: comp.NewSectorCost = 0; break;
                            case 2: comp.NewSectorCost = 2; break;
                            case 3: comp.NewSectorCost = 3; break;
                            case 4: comp.NewSectorCost = 4; break;
                            case 5: comp.NewSectorCost = 4; break;
                            case 6: comp.NewSectorCost = 4; break;
                        }
                    }
                    break;
                case "sector3":
                    if (comp.Sector3)
                    {
                        _audioSystem.PlayPvs(comp.FailSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Этот сектор уже разблокирован", InGameICChatType.Speak, false);
                        break;
                    }
                    if (IsCultistsEnough(uid, 3) && CheckCrystals(uid, comp, comp.NewSectorCost, 0))
                    {
                        comp.Sector3 = true;
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Ритуал открытия нового сектора острова выполнен успешно", InGameICChatType.Speak, false);
                        foreach (var tp in EntityManager.EntityQuery<CultTeleportComponent>())
                        {
                            if (tp.Sector == 3)
                                tp.Enabled = true;
                        }
                        comp.UnlockedSectors++;
                        switch (comp.UnlockedSectors)
                        {
                            case 1: comp.NewSectorCost = 0; break;
                            case 2: comp.NewSectorCost = 2; break;
                            case 3: comp.NewSectorCost = 3; break;
                            case 4: comp.NewSectorCost = 4; break;
                            case 5: comp.NewSectorCost = 4; break;
                            case 6: comp.NewSectorCost = 4; break;
                        }
                    }
                    break;
                case "sector6":
                    if (comp.Sector6)
                    {
                        _audioSystem.PlayPvs(comp.FailSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Этот сектор уже разблокирован", InGameICChatType.Speak, false);
                        break;
                    }
                    if (IsCultistsEnough(uid, 3) && CheckCrystals(uid, comp, comp.NewSectorCost, 0))
                    {
                        comp.Sector6 = true;
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Ритуал открытия нового сектора острова выполнен успешно", InGameICChatType.Speak, false);
                        foreach (var tp in EntityManager.EntityQuery<CultTeleportComponent>())
                        {
                            if (tp.Sector == 6)
                                tp.Enabled = true;
                        }
                        comp.UnlockedSectors++;
                        switch (comp.UnlockedSectors)
                        {
                            case 1: comp.NewSectorCost = 0; break;
                            case 2: comp.NewSectorCost = 2; break;
                            case 3: comp.NewSectorCost = 3; break;
                            case 4: comp.NewSectorCost = 4; break;
                            case 5: comp.NewSectorCost = 4; break;
                            case 6: comp.NewSectorCost = 4; break;
                        }
                    }
                    break;
                case "sector7":
                    if (comp.Sector7)
                    {
                        _audioSystem.PlayPvs(comp.FailSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Этот сектор уже разблокирован", InGameICChatType.Speak, false);
                        break;
                    }
                    if (IsCultistsEnough(uid, 3) && CheckCrystals(uid, comp, comp.NewSectorCost, 0))
                    {
                        comp.Sector7 = true;
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Ритуал открытия нового сектора острова выполнен успешно", InGameICChatType.Speak, false);
                        foreach (var tp in EntityManager.EntityQuery<CultTeleportComponent>())
                        {
                            if (tp.Sector == 7)
                                tp.Enabled = true;
                        }
                        comp.UnlockedSectors++;
                        switch (comp.UnlockedSectors)
                        {
                            case 1: comp.NewSectorCost = 0; break;
                            case 2: comp.NewSectorCost = 2; break;
                            case 3: comp.NewSectorCost = 3; break;
                            case 4: comp.NewSectorCost = 4; break;
                            case 5: comp.NewSectorCost = 4; break;
                            case 6: comp.NewSectorCost = 4; break;
                        }
                    }
                    break;
                case "sector8":
                    if (comp.Sector8)
                    {
                        _audioSystem.PlayPvs(comp.FailSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Этот сектор уже разблокирован", InGameICChatType.Speak, false);
                        break;
                    }
                    if (IsCultistsEnough(uid, 3) && CheckCrystals(uid, comp, comp.NewSectorCost, 0))
                    {
                        comp.Sector8 = true;
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Ритуал открытия нового сектора острова выполнен успешно", InGameICChatType.Speak, false);
                        foreach (var tp in EntityManager.EntityQuery<CultTeleportComponent>())
                        {
                            if (tp.Sector == 8)
                                tp.Enabled = true;
                        }
                        comp.UnlockedSectors++;
                        switch (comp.UnlockedSectors)
                        {
                            case 1: comp.NewSectorCost = 0; break;
                            case 2: comp.NewSectorCost = 2; break;
                            case 3: comp.NewSectorCost = 3; break;
                            case 4: comp.NewSectorCost = 4; break;
                            case 5: comp.NewSectorCost = 4; break;
                            case 6: comp.NewSectorCost = 4; break;
                        }
                    }
                    break;
                case "sector9":
                    if (comp.Sector9)
                    {
                        _audioSystem.PlayPvs(comp.FailSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Этот сектор уже разблокирован", InGameICChatType.Speak, false);
                        break;
                    }
                    if (IsCultistsEnough(uid, 3) && CheckCrystals(uid, comp, comp.NewSectorCost, 0))
                    {
                        comp.Sector9 = true;
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Ритуал открытия нового сектора острова выполнен успешно", InGameICChatType.Speak, false);
                        foreach (var tp in EntityManager.EntityQuery<CultTeleportComponent>())
                        {
                            if (tp.Sector == 9)
                                tp.Enabled = true;
                        }
                        comp.UnlockedSectors++;
                        switch (comp.UnlockedSectors)
                        {
                            case 1: comp.NewSectorCost = 0; break;
                            case 2: comp.NewSectorCost = 2; break;
                            case 3: comp.NewSectorCost = 3; break;
                            case 4: comp.NewSectorCost = 4; break;
                            case 5: comp.NewSectorCost = 4; break;
                            case 6: comp.NewSectorCost = 4; break;
                        }
                    }
                    break;
                case "crystall":
                    if (IsCultistsEnough(uid, 1) && CheckCrystals(uid, comp, 0, 3))
                    {
                        Spawn("MedievalCultCrystallBloody", coords);
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Алые кристаллы успешно конвертированы в кровавый", InGameICChatType.Speak, false);
                    }
                    break;
                case "stable":
                    if (IsCultistsEnough(uid, 1) && CheckCrystals(uid, comp, 0, 0))
                    {
                        double stab = 0f;
                        double speed = 0f;
                        foreach (var barrier in EntityManager.EntityQuery<MagicBarrierComponent>())
                        {
                            stab = Math.Round(barrier.Stability, 2);
                            speed = Math.Round(barrier.Lose, 2);
                        }
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Стабильность барьера " + stab + ", скорость расхода стабильности " + speed, InGameICChatType.Speak, false);
                    }
                    break;
                case "nocturn":
                    if (IsCultistsEnough(uid, 1))
                    {
                        if (TryComp<NocturnComponent>(args.User, out var nocturn))
                        {
                            if (CheckCrystals(uid, comp, 0, 1))
                            {
                                nocturn.BloodLevel = 380f;
                                Spawn("ShockWaveEffect", coords);
                                _audioSystem.PlayPvs(comp.SuccesSound, uid);
                                _chat.TrySendInGameICMessage(uid, "Уровень крови ноктюрна пополнен", InGameICChatType.Speak, false);
                            }
                        }
                        else
                        {
                            _audioSystem.PlayPvs(comp.FailSound, uid);
                            _chat.TrySendInGameICMessage(uid, "Этот ритуал может провести только ноктюрн", InGameICChatType.Speak, false);
                        }
                    }
                    break;
                case "heal":
                    if (IsCultistsEnough(uid, 2) && CheckCrystals(uid, comp, 0, 2))
                    {
                        foreach (var target in _lookup.GetEntitiesInRange(coords, 3.5f))
                        {
                            if (HasComp<CultMemberComponent>(target))
                            {
                                _rejuv.PerformRejuvenate(target);
                            }
                        }
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Все культисты, учавствующие в ритуале, восстановились от ран", InGameICChatType.Speak, false);
                    }
                    break;
                case "food":
                    if (IsCultistsEnough(uid, 2) && CheckCrystals(uid, comp, 0, 1))
                    {
                        Spawn("DrinkWineGlass", coords);
                        Spawn("DrinkWineGlass", coords);
                        Spawn("DrinkWineGlass", coords);
                        Spawn("DrinkWineGlass", coords);
                        Spawn("DrinkWineGlass", coords);
                        Spawn("FoodGrape", coords);
                        Spawn("FoodMeatChickenFried", coords);
                        Spawn("FoodCheese", coords);
                        Spawn("FoodBreadPlain", coords);
                        Spawn("FoodMeatCutletCooked", coords);
                        Spawn("FoodMeatChickenCooked", coords);
                        Spawn("FoodBakedBunMeat", coords);
                        Spawn("FoodGrape", coords);
                        Spawn("FoodMeatChickenFried", coords);
                        Spawn("FoodCheese", coords);
                        Spawn("FoodBreadPlain", coords);
                        Spawn("FoodMeatCutletCooked", coords);
                        Spawn("FoodMeatChickenCooked", coords);
                        Spawn("FoodBakedBunMeat", coords);
                        Spawn("ShockWaveEffect", coords);
                        _audioSystem.PlayPvs(comp.SuccesSound, uid);
                        _chat.TrySendInGameICMessage(uid, "Да будет пиршество", InGameICChatType.Speak, false);
                    }
                    break;
                case "shard":
                    foreach (var center in EntityManager.EntityQuery<CultRitualCenterComponent>())
                    {
                        if (IsCultistsEnough(uid, 2) && CheckCrystals(uid, comp, 0, 0))
                        {
                            var victim = GetHolyItem(center.Owner, "shard");
                            if (victim != center.Owner)
                            {
                                QueueDel(victim);
                                Spawn("MedievalCultCrystallRed", coords);
                                Spawn("MedievalCultCrystallRed", coords);
                                Spawn("MedievalCultCrystallRed", coords);
                                Spawn("ShockWaveEffect", coords);
                                _audioSystem.PlayPvs(comp.SuccesSound, uid);
                                _chat.TrySendInGameICMessage(uid, "Осколок хрусталя успешно преобразован в алые кристаллы", InGameICChatType.Speak, false);
                            }
                        }
                    }
                    break;
                case "foliant":
                    foreach (var center in EntityManager.EntityQuery<CultRitualCenterComponent>())
                    {
                        if (IsCultistsEnough(uid, 2) && CheckCrystals(uid, comp, 0, 0))
                        {
                            var victim = GetHolyItem(center.Owner, "foliant");
                            if (victim != center.Owner)
                            {
                                QueueDel(victim);
                                Spawn("MedievalCultCrystallRed", coords);
                                Spawn("MedievalCultCrystallRed", coords);
                                Spawn("MedievalCultCrystallRed", coords);
                                Spawn("MedievalCultCrystallRed", coords);
                                Spawn("MedievalCultCrystallRed", coords);
                                Spawn("ShockWaveEffect", coords);
                                _audioSystem.PlayPvs(comp.SuccesSound, uid);
                                _chat.TrySendInGameICMessage(uid, "Проклятый фолиант успешно преобразован кристаллы", InGameICChatType.Speak, false);
                            }
                        }
                    }
                    break;
                case "manaregen":
                    if (IsCultistsEnough(uid, 1) && CheckCrystals(uid, comp, 1, 2))
                    {
                        if (TryComp<ManaComponent>(args.User, out var mana) && mana.MaxManaRaceModifier != 0)
                        {
                            mana.Regen *= 1.25f;
                            Spawn("ShockWaveEffect", coords);
                            _audioSystem.PlayPvs(comp.SuccesSound, uid);
                            _chat.TrySendInGameICMessage(uid, "Скорость восстановления маны у проводящего ритуал повышена", InGameICChatType.Speak, false);
                        }
                        else
                        {
                            _audioSystem.PlayPvs(comp.FailSound, uid);
                            _chat.TrySendInGameICMessage(uid, "Проводящий ритуал не может колдовать", InGameICChatType.Speak, false);
                        }

                    }
                    break;

                default:
                    _chat.TrySendInGameICMessage(uid, "Руна выполненна неверно, покайтесь!", InGameICChatType.Speak, false);
                    _damageableSystem.TryChangeDamage(cultistritualist.Owner, cultistritualist.Damage, true, false);
                    break;
            }
            foreach (var rune in EntityManager.EntityQuery<CultBloodPaintComponent>())
            {
                if (rune.Bloody)
                {
                    EnsureComp<TimedDespawnComponent>(rune.Owner, out var desp);
                    desp.Lifetime = 0.01f;
                }
            }

        }

        private bool CheckCrystals(EntityUid uid, CultCheckPictureComponent comp, int bloodyCost, int redCost)
        {
            if (comp.BloodyCrystall < bloodyCost)
            {
                _chat.TrySendInGameICMessage(uid, $"Для ритуала недостаточно кровавых кристаллов, необходимо {bloodyCost}", InGameICChatType.Speak, false);
                _audioSystem.PlayPvs(comp.FailSound, uid);
                return false;
            }

            if (comp.RedCrystall < redCost)
            {
                _chat.TrySendInGameICMessage(uid, $"Для ритуала недостаточно алых кристаллов, необходимо {redCost}", InGameICChatType.Speak, false);
                _audioSystem.PlayPvs(comp.FailSound, uid);
                return false;
            }

            comp.BloodyCrystall -= bloodyCost;
            comp.RedCrystall -= redCost;
            return true;
        }

        private void OnExamine(EntityUid uid, CultCheckPictureComponent comp, ExaminedEvent args)
        {
            args.PushMarkup("Сейчас заряжено [color=red]" + comp.BloodyCrystall.ToString() + " кровавых[/color] и [color=pink]" + comp.RedCrystall.ToString() + " алых[/color] кристаллов");
        }
        private void OnExamineTp(EntityUid uid, CultTeleportComponent comp, ExaminedEvent args)
        {
            if (HasComp<CultMemberComponent>(args.Examiner))
            {
                if (!comp.Enabled)
                    args.PushMarkup("Этот телепорт сейчас [color=red] выключен[/color], проведите специальный ритуал для его разблокировки");
                else
                    args.PushMarkup("Этот телепорт сейчас [color=green] включен[/color]. Если вы культист - ударьте себя ножом, чтобы телепортировать всех в радиусе двух тайлов к связанному с этим телепорту.");
            }
            else
                args.PushMarkup("Вы и [color=red] понятия не имеете[/color] что это за штука, но четко понимаете, что она как-то связана с кровавым культом, называющим себя культом истины.");
        }

        private void OnExamineCursed(EntityUid uid, CultCursedComponent comp, ExaminedEvent args)
        {
            if (HasComp<CultMemberComponent>(args.Examiner))
            {
                if (comp.CurseLevel > 0f)
                    args.PushMarkup("Имеет [color=red]связь с культом[/color]");
                if (comp.CurseLevel <= 0f)
                    args.PushMarkup("[color=red]Разорвал[/color] связь с культом, грешник!");
            }
            if (TryComp<CultCursedComponent>(args.Examiner, out var cursed) && cursed.CurseLevel > 0f && comp.CurseLevel > 0f)
                args.PushMarkup("Тоже имеет [color=red]связь с культом[/color]");
        }

        public bool IsCultistsEnough(EntityUid uid, int need)
        {
            foreach (var center in EntityManager.EntityQuery<CultRitualCenterComponent>())
            {
                if (GetCultistCount(center.Owner) < need)
                {
                    _chat.TrySendInGameICMessage(uid, "Руна верна, недостаточно членов культа для ритуала, необходимо минимум " + need.ToString(), InGameICChatType.Speak, false);
                    return false;
                }
            }
            return true;
        }

        public int GetCultistCount(EntityUid center)
        {
            var xform = Transform(center);
            var coords = xform.Coordinates;
            int count = 0;
            foreach (var target in _lookup.GetEntitiesInRange(coords, 3.5f))
            {
                if (HasComp<CultMemberComponent>(target))
                {
                    count++;
                }
            }
            return count;
        }

        public EntityUid GetVictim(EntityUid center)
        {
            var xform = Transform(center);
            var coords = xform.Coordinates;
            foreach (var target in _lookup.GetEntitiesInRange(coords, 3.5f))
            {
                if (!HasComp<CultMemberComponent>(target) && HasComp<MedievalSpikeTargetComponent>(target))
                {
                    return target;
                }
            }
            return center;
        }


        public EntityUid GetHolyItem(EntityUid center, string holyType)
        {
            var xform = Transform(center);
            var coords = xform.Coordinates;
            foreach (var target in _lookup.GetEntitiesInRange(coords, 3.5f))
            {
                if (TryComp<CultHolyItemComponent>(target, out var holy) && holy.HolyItemType == holyType)
                {
                    return target;
                }
            }
            return center;
        }
        public void OnUseBrushInHand(EntityUid uid, CultBrushComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUseBrush(args.Target, args.User, args.Used, comp);
        }

        public void OnUseBrush(EntityUid? target, EntityUid user, EntityUid used, CultBrushComponent comp)
        {
            if (target == null)
                return;
            if (TryComp<CultBloodPaintComponent>(target, out var paint) && paint != null)
            {
                var xform = Transform(target.Value);
                var coords = xform.Coordinates;
                if (!paint.Bloody)
                {

                    var newBrush = Spawn("MedievalCultBrushBloody", coords);
                    _audioSystem.PlayPvs(comp.Sound, newBrush);
                    EnsureComp<CultBloodPaintComponent>(newBrush, out var newPaint);
                    newPaint.PosX = paint.PosX;
                    newPaint.PosY = paint.PosY;
                }
                else
                {
                    var newBrush = Spawn("MedievalCultBrushFine", coords);
                    _audioSystem.PlayPvs(comp.DelSound, newBrush);
                    EnsureComp<CultBloodPaintComponent>(newBrush, out var newPaint);
                    newPaint.PosX = paint.PosX;
                    newPaint.PosY = paint.PosY;
                }
                QueueDel(target);
            }
        }
        public void OnUseCrystallInHand(EntityUid uid, CultCrystallComponent comp, BeforeRangedInteractEvent args)
        {
            if (!args.CanReach)
                return;
            OnUseCrystall(args.Target, args.User, args.Used, comp);
        }

        public void OnUseCrystall(EntityUid? target, EntityUid user, EntityUid used, CultCrystallComponent comp)
        {
            if (target == null)
                return;
            if (TryComp<CultCheckPictureComponent>(target, out var pict) && pict != null)
            {
                if (comp.Bloody)
                    pict.BloodyCrystall += 1;
                else
                    pict.RedCrystall += 1;
                _audioSystem.PlayPvs(pict.EatSound, target.Value);
                QueueDel(used);
            }
        }
        public string GetRuneFigure()
        {
            var runeCoordinates = FindRuneCoordinates();

            if (CheckForChrist(runeCoordinates))
                return "christ"; // накладывание на человека эффекта проклятой метки
            if (CheckForAxe(runeCoordinates))
                return "axe"; // призыв оружия
            if (CheckForBoat(runeCoordinates))
                return "boat"; // дамаг по барьеру
            if (CheckForKey(runeCoordinates))
                return "key"; // открытие финального телепорта ПОСЛе того, как культисты открыли для себя все секторы
            if (CheckForSwordShield(runeCoordinates))
                return "swordshield"; // покупка брони
            if (CheckForWizard(runeCoordinates))
                return "wizard"; // покупка гримуара
            if (CheckForHeart(runeCoordinates))
                return "heart"; // покупка камня возрождения
            if (CheckForScroll(runeCoordinates))
                return "scroll"; // призыв проклятого свитка, которого надо отнести к барьеру, анлок ПОСЛЕ ключа только
            if (CheckForWand(runeCoordinates))
                return "wand"; // призыв гримуара
            if (CheckForSector1(runeCoordinates))
                return "sector1"; //сектор
            if (CheckForSector2(runeCoordinates))
                return "sector2"; //сектор
            if (CheckForSector3(runeCoordinates))
                return "sector3"; //сектор
            if (CheckForSector6(runeCoordinates))
                return "sector6"; //сектор
            if (CheckForSector7(runeCoordinates))
                return "sector7"; //сектор
            if (CheckForSector8(runeCoordinates))
                return "sector8"; //сектор
            if (CheckForSector9(runeCoordinates))
                return "sector9"; //сектор
            if (CheckForCrystall(runeCoordinates))
                return "crystall"; //кристалл
            if (CheckForStable(runeCoordinates))
                return "stable"; //стабильность
            if (CheckForNocturn(runeCoordinates))
                return "nocturn"; //Ноктюрн
            if (CheckForHeal(runeCoordinates))
                return "heal"; //Лечение
            if (CheckForFood(runeCoordinates))
                return "food"; //еда
            if (CheckForShard(runeCoordinates))
                return "shard"; //осколок
            if (CheckForFoliant(runeCoordinates))
                return "foliant"; //Книга
            if (CheckForManaRegen(runeCoordinates))
                return "manaregen"; //Восстановление маны

            return "nothing";
        }

        private List<(int X, int Y)> FindRuneCoordinates()
        {
            List<(int X, int Y)> coordinates = new List<(int X, int Y)>();

            foreach (var rune in EntityManager.EntityQuery<CultBloodPaintComponent>())
            {
                if (rune.Bloody)
                {
                    coordinates.Add((rune.PosX, rune.PosY));
                }
            }
            return coordinates;
        }

        public bool IsInCoords(List<(int X, int Y)> coordinates, int CellX, int CellY)
        {
            foreach (var coordinate in coordinates)
            {
                if (coordinate.ToString() == "(" + CellX + ", " + CellY + ")") return true;
            }
            return false;
        }

        private bool CheckFigure(List<(int X, int Y)> coordinates, List<(int X, int Y)> whitePixels)
        {
            var newCoordinates = coordinates.ToList();

            foreach (var pixel in whitePixels)
            {
                if (!IsInCoords(newCoordinates, pixel.X, pixel.Y))
                    return false;
                else
                    newCoordinates.Remove((pixel.X, pixel.Y)); // Исправление: Создаём новый ValueTuple
            }

            if (newCoordinates.Any()) // Более лаконичная проверка на пустоту
                return false;
            return true;
        }

        private bool CheckForChrist(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
        (2, 1), (6, 1), (9, 1),
        (1, 2), (5, 2), (6, 2), (7, 2), (10, 2),
        (6, 3),
        (3, 4), (4, 4), (5, 4), (6, 4), (7, 4), (8, 4), (9, 4),
        (6, 5), (7, 5),
        (6, 6), (8, 6),
        (6, 7), (7, 7),
        (5, 8), (6, 8),
        (1, 9), (4, 9), (6, 9), (10, 9),
        (2, 10), (5, 10), (6, 10), (9, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForAxe(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
        (3, 1), (5, 1), (7, 1),
        (2, 2), (5, 2), (8, 2),
        (2, 3), (3, 3), (4, 3), (5, 3), (6, 3), (7, 3), (8, 3),
        (2, 4), (3, 4), (4, 4), (5, 4), (6, 4), (7, 4), (8, 4),
        (2, 5), (5, 5), (8, 5),
        (3, 6), (5, 6), (7, 6),
        (5, 7), (9, 7), (10, 7),
        (4, 8), (5, 8), (6, 8), (7, 8), (8, 8), (9, 8), (10, 8),
        (4, 9), (5, 9), (6, 9), (7, 9), (8, 9), (9, 9), (10, 9),
        (5, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForBoat(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
        (5, 1), (6, 1),
        (2, 2), (5, 2), (6, 2), (9, 2),
        (3, 3), (4, 3), (5, 3), (6, 3), (7, 3), (8, 3),
        (2, 4), (5, 4), (6, 4), (9, 4),
        (1, 5), (5, 5), (6, 5), (10, 5),
        (2, 6), (5, 6), (6, 6), (9, 6),
        (3, 7), (4, 7), (5, 7), (6, 7), (7, 7), (8, 7),
        (2, 8), (5, 8), (6, 8), (9, 8),
        (4, 9), (7, 9),
        (3, 10), (4, 10), (7, 10), (8, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForKey(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
        (2, 1), (3, 1),
        (1, 2), (4, 2), (6, 2), (7, 2), (8, 2), (9, 2),
        (1, 3), (4, 3), (8, 3),
        (2, 4), (3, 4), (7, 4),
        (2, 5), (8, 5),
        (2, 6), (7, 6),
        (2, 7), (3, 7), (4, 7), (8, 7),
        (2, 8), (7, 8),
        (1, 9), (2, 9), (3, 9), (4, 9), (6, 9), (7, 9), (8, 9), (9, 9)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForSwordShield(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
        (3, 1), (6, 1), (8, 1), (10, 1),
        (2, 2), (3, 2),
        (2, 3), (6, 3), (8, 3), (10, 3),
        (2, 4), (3, 4), (6, 4), (7, 4), (8, 4), (9, 4), (10, 4),
        (3, 5), (6, 5), (7, 5), (8, 5), (9, 5), (10, 5),
        (2, 6), (3, 6), (6, 6), (7, 6), (8, 6), (9, 6), (10, 6),
        (2, 7), (7, 7), (8, 7), (9, 7),
        (1, 8), (2, 8), (3, 8), (4, 8),(8, 8),
        (2, 9), (3, 9),
        (2, 10), (3, 10), (8, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForWizard(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
        (1, 1), (9, 1),
        (1, 2), (3, 2), (4, 2), (5, 2), (8, 2), (9, 2), (10, 2),
        (3, 3), (4, 3), (5, 3), (6, 3), (9, 3),
        (2, 4), (4, 4), (5, 4), (6, 4), (9, 4),
        (4, 5), (5, 5), (6, 5), (7, 5),
        (4, 6), (5, 6), (6, 6), (7, 6), (10, 6),
        (1, 7), (3, 7), (4, 7), (7, 7), (8, 7), (10, 7),
        (1, 8), (3, 8), (4, 8), (7, 8), (8, 8),
        (3, 9), (4, 9), (5, 9), (6, 9), (7, 9), (8, 9),
        (2, 10), (3, 10), (4, 10), (5, 10), (6, 10), (7, 10), (8, 10), (9, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForHeart(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
        (2, 1), (9, 1),
        (1, 2), (3, 2), (4, 2), (7, 2), (8, 2), (10, 2),
        (2, 3), (5, 3), (6, 3), (9, 3),
        (2, 4), (4, 4), (7, 4), (9, 4),
        (2, 5), (4, 5), (7, 5), (9, 5),
        (2, 6), (5, 6), (6, 6), (9, 6),
        (3, 7), (8, 7),
        (2, 8), (4, 8), (7, 8), (9, 8),
        (2, 9), (3, 9), (5, 9), (6, 9), (8, 9), (9, 9),
        (1, 10), (10, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForScroll(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
        (3, 2), (4, 2), (5, 2), (6, 2), (7, 2), (8, 2),
        (2, 3), (9, 3),
        (3, 4), (4, 4), (5, 4), (6, 4), (7, 4), (8, 4),
        (3, 5), (8, 5),
        (3, 6), (5, 6), (6, 6), (8, 6),
        (3, 7), (8, 7),
        (3, 8), (6, 8), (7, 8), (8, 8),
        (3, 9), (4, 9), (5, 9)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForWand(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
        (1, 1), (10, 1),
        (3, 2), (6, 2), (8, 2),
        (2, 3), (4, 3), (7, 3), (8, 3), (9, 3),
        (3, 4), (6, 4), (7, 4), (8, 4),
        (1, 5), (6, 5), (7, 5), (9, 5),
        (5, 6),
        (4, 7), (8, 7),
        (3, 8), (7, 8), (9, 8),
        (2, 9), (8, 9),
        (1, 10), (6, 10), (10, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForSector1(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
(2, 1), (3, 1),
(1, 2), (4, 2),
(1, 3), (4, 3),
(2, 4), (3, 4),
(5, 5), (6, 5),
(5, 6), (6, 6)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForSector2(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
(5, 1), (6, 1),
(4, 2), (7, 2),
(4, 3), (7, 3),
(5, 4), (6, 4),
(5, 5), (6, 5),
(5, 6), (6, 6)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForSector3(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
(8, 1), (9, 1),
(7, 2), (10, 2),
(7, 3), (10, 3),
(8, 4), (9, 4),
(5, 5), (6, 5),
(5, 6), (6, 6)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForSector6(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
(8, 4), (9, 4),
(5, 5), (6, 5), (7, 5), (10, 5),
(5, 6), (6, 6), (7, 6), (10, 6),
(8, 7), (9, 7)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForSector7(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
(6, 5), (5, 5),
(6, 6), (5, 6),
(2, 7), (3, 7),
(1, 8), (4, 8),
(1, 9), (4, 9),
(2, 10), (3, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForSector8(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
(6, 5), (5, 5),
(6, 6), (5, 6),
(6, 7), (5, 7),
(4, 8), (7, 8),
(4, 9), (7, 9),
(6, 10), (5, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForSector9(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
(6, 5), (5, 5),
(6, 6), (5, 6),
(8, 7), (9, 7),
(7, 8), (10, 8),
(7, 9), (10, 9),
(8, 10), (9, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForCrystall(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
(5, 1), (6, 1),
(2, 2), (6, 2), (5, 2), (9, 2),
(7, 3), (4, 3), (5, 3), (6, 3),
(3, 4), (5, 4), (6, 4), (8, 4),
(2, 5), (4, 5), (7, 5), (9, 5),
(2, 6), (4, 6), (7, 6), (9, 6),
(2, 7), (5, 7), (6, 7), (9, 7),
(1, 8), (3, 8), (8, 8), (10, 8),
(4, 9), (5, 9), (6, 9), (7, 9),
(3, 10), (8, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForStable(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
(8, 1), (9, 1),
(3, 2), (7, 2), (10, 2),
(2, 3), (3, 3), (4, 3), (7, 3), (10, 3),
(3, 4), (7, 4), (10, 4),
(3, 5), (6, 5), (8, 5), (9, 5),
(3, 6), (6, 6), (8, 6),
(3, 7), (5, 7),
(3, 8), (4, 8), (8, 8),
(3, 9)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForNocturn(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
(2, 2), (5, 2), (6, 2), (9, 2),
(4, 3), (7, 3),
(2, 4), (4, 4), (5, 4), (6, 4), (7, 4), (9, 4),
(1, 5), (2, 5), (3, 5), (4, 5), (7, 5), (8, 5), (9, 5), (10, 5),
(2, 6), (4, 6), (7, 6), (9, 6),
(3, 7), (5, 7), (6, 7), (8, 7),
(1, 8), (3, 8), (4, 8), (7, 8), (8, 8), (10, 8),
(3, 9), (8, 9),
(4, 10), (5, 10), (6, 10), (7, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForHeal(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
(2, 1), (10, 1),
(1, 2), (2, 2), (7, 2), (8, 2), (9, 2),
(4, 3), (6, 3), (9, 3),
(3, 4), (5, 4), (9, 4),
(4, 5), (6, 5), (8, 5),
(3, 6), (7, 6),
(2, 7), (6, 7), (8, 7),
(2, 8), (5, 8), (7, 8),
(2, 9), (3, 9), (4, 9), (9, 9), (10, 9),
(1, 10), (9, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForFood(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
(4, 1), (7, 1), (8, 1), (9, 1), (10, 1),
(2, 2), (6, 2), (10, 2),
(6, 3),
(3, 4), (4, 4), (7, 4), (8, 4),
(2, 5), (4, 5), (6, 5), (7, 5), (9, 5),
(3, 6), (4, 6), (5, 6), (6, 6), (7, 6), (8, 6),
(4, 7), (5, 7), (6, 7), (7, 7), (10, 7),
(2, 8), (5, 8), (6, 8),
(5, 9), (6, 9), (9, 9),
(4, 10), (5, 10), (6, 10), (7, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForShard(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
(3, 1), (8, 1),
(4, 2), (7, 2),
(1, 3), (4, 3), (5, 3), (6, 3), (7, 3), (10, 3),
(2, 4), (3, 4), (5, 4), (6, 4), (8, 4), (9, 4),
(1, 5), (3, 5), (8, 5), (10, 5),
(3, 6), (8, 6),
(2, 7), (3, 7), (5, 7), (6, 7), (8, 7), (9, 7),
(1, 8), (4, 8), (5, 8), (6, 8), (7, 8), (10, 8),
(6, 9), (5, 9),
(6, 10), (5, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForFoliant(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
(4, 1), (7, 1),
(3, 2), (4, 2), (5, 2), (6, 2), (7, 2), (8, 2),
(2, 3), (5, 3), (6, 3), (9, 3),
(3, 4), (5, 4), (6, 4), (8, 4),
(4, 5), (7, 5),
(2, 6), (3, 6), (4, 6), (7, 6), (8, 6), (9, 6),
(2, 7), (5, 7), (6, 7), (9, 7),
(4, 8), (5, 8), (6, 8), (7, 8),
(3, 9), (4, 9), (5, 9), (6, 9), (7, 9), (8, 9),
(6, 10), (5, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }

        private bool CheckForManaRegen(List<(int X, int Y)> coordinates)
        {
            var whitePixels = new List<(int X, int Y)>
    {
(2, 1), (9, 1),
(1, 2), (2, 2), (5, 2), (6, 2), (9, 2), (10, 2),
(2, 3), (4, 3), (6, 3), (9, 3),
(1, 4), (4, 4), (5, 4), (6, 4), (7, 4), (10, 4),
(3, 5), (8, 5),
(4, 6), (7, 6),
(6, 7), (5, 7),
(1, 8), (4, 8), (6, 8), (10, 8),
(1, 9), (2, 9), (9, 9), (10, 9),
(4, 10), (5, 10), (6, 10), (7, 10)
    };

            return CheckFigure(coordinates, whitePixels);
        }


    }
}
