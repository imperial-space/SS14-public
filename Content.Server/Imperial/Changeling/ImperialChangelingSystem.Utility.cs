using System.Linq;
using Content.Server.Administration;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Imperial.Disease;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Drunk;
using Content.Shared.Emp;
using Content.Shared.Ensnaring;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Examine;
using Content.Shared.Flash.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Changeling;
using Content.Shared.Imperial.Disease;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;
using Content.Shared.Imperial.Vision;
using Content.Shared.Jittering;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Medical;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Stunnable;
using Content.Shared.Temperature.Components;
using Content.Shared.Zombies;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Changeling;

/// <summary>
/// Прочие способности TG: Gene Stim, глаза, биодеградация, хамелеон, тьма, дефиб-хват, цифровой камуфляж,
/// fleshmend, панацея, феромоны, два крика, пауки, мышцы, пустота, имитация голоса.
/// </summary>
public sealed partial class ImperialChangelingSystem
{
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly SharedPoweredLightSystem _lights = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedDrunkSystem _drunk = default!;
    [Dependency] private readonly SharedStutteringSystem _stutter = default!;
    [Dependency] private readonly VomitSystem _vomit = default!;
    [Dependency] private readonly DiseaseSystem _disease = default!;
    [Dependency] private readonly SharedCuffableSystem _cuffs = default!;
    [Dependency] private readonly SharedEnsnareableSystem _ensnare = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly Content.Server.Chat.Systems.ChatSystem _chatSystem = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly Content.Shared.Storage.EntitySystems.SharedEntityStorageSystem _storage = default!;

    public const string AdrenalinePower = "ImperialChangelingAdrenaline";
    public const string EyesightPower = "ImperialChangelingEyesight";
    public const string BiodegradePower = "ImperialChangelingBiodegrade";
    public const string ChameleonPower = "ImperialChangelingChameleon";
    public const string DarknessPower = "ImperialChangelingDarkness";
    public const string DefibGraspPower = "ImperialChangelingDefibGrasp";
    public const string DigitalCamoPower = "ImperialChangelingDigitalCamo";
    public const string FleshmendPower = "ImperialChangelingFleshmend";
    public const string PanaceaPower = "ImperialChangelingPanacea";
    public const string PheromonePower = "ImperialChangelingPheromone";
    public const string ResonantShriekPower = "ImperialChangelingResonantShriek";
    public const string TechnoShriekPower = "ImperialChangelingTechnoShriek";
    public const string SpidersPower = "ImperialChangelingSpiders";
    public const string MusclesPower = "ImperialChangelingMuscles";
    public const string VoidPower = "ImperialChangelingVoidAdaption";
    public const string MimicVoicePower = "ImperialChangelingMimicVoice";

    private static readonly SoundSpecifier Screech = new SoundPathSpecifier("/Audio/Imperial/Uplink/screech.ogg");
    private static readonly SoundSpecifier Welder = new SoundPathSpecifier("/Audio/Items/welder.ogg");
    private static readonly SoundSpecifier DemonConsume = new SoundPathSpecifier("/Audio/Imperial/Changeling/demon_consume.ogg");
    private static readonly SoundSpecifier DefibZap = new SoundPathSpecifier("/Audio/Items/Defib/defib_zap.ogg");
    private static readonly SoundSpecifier DefibSuccess = new SoundPathSpecifier("/Audio/Items/Defib/defib_success.ogg");
    private static readonly SoundSpecifier BodyFall = new SoundCollectionSpecifier("BodyFall");
    private static readonly SoundSpecifier AcidSpit = new SoundPathSpecifier("/Audio/Imperial/Changeling/bileworm_spit.ogg");
    private static readonly EntProtoId SpiderEggs = "ImperialChangelingSpiderEggs";
    private static readonly Robust.Shared.Prototypes.ProtoId<Content.Shared.Alert.AlertPrototype> FleshmendAlert = "ImperialChangelingFleshmend";

    /// <summary>darkness_adaptation/recharge_slowdown, pheromone_receptors и mimic_voice: 0.25, void_adaption: 0.25.</summary>
    private const float DarknessSlowdown = 0.15f;
    private const float PheromoneSlowdown = 0.25f;
    private const float MimicSlowdown = 0.25f;
    private const float VoidSlowdown = 0.25f;

    /// <summary>CHANGELING_PHEROMONE_MIN_DISTANCE / MAX_DISTANCE, range_mid / range_far.</summary>
    private const float PheromoneMin = 10;
    private const float PheromoneMax = 25;
    private const float PheromoneMid = 8;
    private const float PheromoneFar = 16;

    /// <summary>strained_muscles: multiplicative_slowdown = -0.55.</summary>
    private const float MusclesSpeed = 1.5f;

    /// <summary>Генокрады, у которых мы убрали дыхание (void_adaption): вернуть из прототипа.</summary>
    private readonly HashSet<EntityUid> _removedRespirator = new();

    private readonly Dictionary<EntityUid, float> _oldColdThreshold = new();

    private void InitializeUtility()
    {
        _powers[AdrenalinePower] = UseAdrenaline;
        _powers[EyesightPower] = UseEyesight;
        _powers[BiodegradePower] = UseBiodegrade;
        _powers[ChameleonPower] = UseChameleon;
        _powers[DarknessPower] = UseDarkness;
        _powers[DigitalCamoPower] = UseDigitalCamo;
        _powers[FleshmendPower] = UseFleshmend;
        _powers[PanaceaPower] = UsePanacea;
        _powers[PheromonePower] = UsePheromone;
        _powers[ResonantShriekPower] = UseResonantShriek;
        _powers[TechnoShriekPower] = UseTechnoShriek;
        _powers[SpidersPower] = UseSpiders;
        _powers[MusclesPower] = UseMuscles;
        _powers[MimicVoicePower] = UseMimicVoice;

        _powerGranted[VoidPower] = GrantVoid;

        _powerRemoved[EyesightPower] = ent =>
        {
            _eye.SetDrawFov(ent, true);
            ent.Comp.Toggled.Remove(EyesightPower);
        };
        _powerRemoved[ChameleonPower] = ent => SetChameleon(ent, false);
        _powerRemoved[DarknessPower] = ent => SetDarkness(ent, false);
        _powerRemoved[DigitalCamoPower] = ent => ent.Comp.Toggled.Remove(DigitalCamoPower);
        _powerRemoved[PheromonePower] = ent => SetPheromone(ent, false);
        _powerRemoved[MusclesPower] = ent => SetMuscles(ent, false);
        _powerRemoved[MimicVoicePower] = ent => SetMimic(ent, null);
        _powerRemoved[VoidPower] = RemoveVoid;

        SubscribeLocalEvent<ImperialChangelingComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<ImperialChangelingComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ImperialChangelingComponent, TargetBeforeDefibrillatorZapsEvent>(OnDefibZap);
        SubscribeLocalEvent<ImperialChangelingComponent, Content.Shared.Flash.FlashAttemptEvent>(OnFlashAttempt);
    }

    private void UpdateUtility(Entity<ImperialChangelingComponent> ent, TimeSpan now)
    {
        var uid = ent.Owner;

        // /datum/status_effect/fleshmend: 10 секунд, не лечит в огне.
        if (ent.Comp.FleshmendUntil is { } until)
        {
            if (now >= until)
            {
                ent.Comp.FleshmendUntil = null;
                _alerts.ClearAlert(uid, FleshmendAlert);
            }
            else if (!(TryComp<Content.Shared.Atmos.Components.FlammableComponent>(uid, out var flammable) && flammable.OnFire))
            {
                var heal = new DamageSpecifier(_proto.Index<DamageGroupPrototype>(new Robust.Shared.Prototypes.ProtoId<DamageGroupPrototype>("Brute")), -4);
                heal += new DamageSpecifier(_proto.Index<DamageGroupPrototype>(new Robust.Shared.Prototypes.ProtoId<DamageGroupPrototype>("Burn")), -2);
                heal += new DamageSpecifier(_proto.Index<DamageGroupPrototype>(new Robust.Shared.Prototypes.ProtoId<DamageGroupPrototype>("Airloss")), -4);
                _damage.TryChangeDamage(uid, heal, true, false);
            }

            ShowFleshmendAlert(ent);
        }

        // strained_muscles/muscle_loop(): раз в 4 секунды, отдых — раз в 2.
        if (now >= ent.Comp.NextMuscleTick)
        {
            if (ent.Comp.Toggled.Contains(MusclesPower))
            {
                ent.Comp.NextMuscleTick = now + TimeSpan.FromSeconds(4);
                var stamina = TryComp<Content.Shared.Damage.Components.StaminaComponent>(uid, out var stam) ? _stamina.GetStaminaDamage(uid, stam) : 0;
                if (!_mobState.IsAlive(uid) || stamina >= 90)
                {
                    Popup(uid, "imperial-changeling-muscles-exhausted");
                    _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(4));
                    SetMuscles(ent, false, quiet: true);
                }
                else
                {
                    ent.Comp.MuscleStacks++;
                    _stamina.TakeStaminaDamage(uid, ent.Comp.MuscleStacks * 1.3f);
                    if (ent.Comp.MuscleStacks == 11)
                        Popup(uid, "imperial-changeling-muscles-pain");
                }
            }
            else if (ent.Comp.MuscleStacks > 0)
            {
                ent.Comp.NextMuscleTick = now + TimeSpan.FromSeconds(2);
                ent.Comp.MuscleStacks--;
            }
        }

        // agent_pinpointer/changeling: раз в 2 секунды.
        if (ent.Comp.Toggled.Contains(PheromonePower) && now >= ent.Comp.NextPheromoneTick)
        {
            ent.Comp.NextPheromoneTick = now + TimeSpan.FromSeconds(2);
            UpdatePheromone(ent);
        }

        if (ent.Comp.Purchased.Contains(VoidPower))
            UpdateVoid(ent);

        // darkness_adapted/tick()
        if (ent.Comp.Toggled.Contains(DarknessPower))
            UpdateDarkness(ent);
    }

    #region Gene Stim и панацея

    /// <summary>adrenaline: встать, сбросить усталость, 4 ед. адреналина генокрада.</summary>
    private bool UseAdrenaline(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        var uid = ent.Owner;
        _stun.TryUpdateStunDuration(uid, TimeSpan.Zero);
        _stun.TryUpdateParalyzeDuration(uid, TimeSpan.Zero);
        RemComp<KnockedDownComponent>(uid);
        if (TryComp<Content.Shared.Damage.Components.StaminaComponent>(uid, out var stamina))
            _stamina.TakeStaminaDamage(uid, -_stamina.GetStaminaDamage(uid, stamina), stamina);

        _bloodstream.TryAddToBloodstream(uid, new Solution("ImperialChangelingAdrenaline", 4));
        Popup(uid, "imperial-changeling-adrenaline");
        return true;
    }

    /// <summary>panacea: паразиты, болезни, мутации, токсины.</summary>
    private bool UsePanacea(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        var uid = ent.Owner;
        Popup(uid, "imperial-changeling-panacea");

        // body_egg, legion_tumour, zombie_infection: вырвать наружу.
        var vomit = false;
        if (RemComp<Content.Shared.Imperial.Xenomorph.XenoEmbryoComponent>(uid))
            vomit = true;
        if (RemComp<PendingZombieComponent>(uid))
        {
            RemComp<ZombifyOnDeathComponent>(uid);
            vomit = true;
        }

        if (vomit)
            _vomit.Vomit(uid, 0, 0, force: true);

        var chems = new Solution();
        chems.AddReagent(new Robust.Shared.Prototypes.ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype>("Mutadone"), 10);
        chems.AddReagent(new Robust.Shared.Prototypes.ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype>("Arithrazine"), 20);
        chems.AddReagent(new Robust.Shared.Prototypes.ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype>("Antihol"), 10);
        chems.AddReagent(new Robust.Shared.Prototypes.ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype>("Mannitol"), 25);
        _bloodstream.TryAddToBloodstream(uid, chems);

        if (TryComp<DiseasedComponent>(uid, out var diseased))
        {
            foreach (var disease in diseased.Diseases.ToList())
            {
                if (disease.Severity != DiseaseSeverity.Positive)
                    _disease.Cure(uid, disease);
            }
        }

        return true;
    }

    /// <summary>/atom/movable/screen/alert/status_effect/fleshmend: show_duration, fleshmend_fire в огне.</summary>
    private void ShowFleshmendAlert(Entity<ImperialChangelingComponent> ent)
    {
        if (ent.Comp.FleshmendUntil is not { } until)
            return;

        var burning = TryComp<Content.Shared.Atmos.Components.FlammableComponent>(ent, out var flammable) && flammable.OnFire;
        _alerts.ShowAlert(ent.Owner, FleshmendAlert, (short) (burning ? 2 : 1), (until - TimeSpan.FromSeconds(10), until));
    }

    /// <summary>fleshmend: лечение раз в секунду 10 секунд.</summary>
    private bool UseFleshmend(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        if (ent.Comp.FleshmendUntil != null)
        {
            Popup(ent, "imperial-changeling-fleshmend-already");
            return false;
        }

        Popup(ent, "imperial-changeling-fleshmend");
        ent.Comp.FleshmendUntil = _timing.CurTime + TimeSpan.FromSeconds(10);
        ShowFleshmendAlert(ent);
        return true;
    }

    #endregion

    #region Глаза, хамелеон, тьма, камуфляж

    /// <summary>augmented_eyesight: рентген (без стен в обзоре) или защита от вспышек.</summary>
    private bool UseEyesight(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        var uid = ent.Owner;
        if (ent.Comp.Toggled.Remove(EyesightPower))
        {
            _eye.SetDrawFov(uid, true);
            Popup(uid, "imperial-changeling-eyesight-protect");
        }
        else
        {
            ent.Comp.Toggled.Add(EyesightPower);
            _eye.SetDrawFov(uid, false);
            Popup(uid, "imperial-changeling-eyesight-xray");
        }

        SetPowerToggled(ent, EyesightPower, ent.Comp.Toggled.Contains(EyesightPower));

        Dirty(ent);
        return true;
    }

    /// <summary>
    /// augmented_eyesight: пока рентген выключен, глаза защищены как сварочной маской (FLASH_PROTECTION_WELDER).
    /// Свой обработчик вместо FlashImmunity, чтобы осмотр не выдавал генокрада.
    /// </summary>
    private void OnFlashAttempt(Entity<ImperialChangelingComponent> ent, ref Content.Shared.Flash.FlashAttemptEvent args)
    {
        if (ent.Comp.Purchased.Contains(EyesightPower) && !ent.Comp.Toggled.Contains(EyesightPower))
            args.Cancelled = true;
    }

    /// <summary>chameleon_skin: невидимость, пока стоим на месте.</summary>
    private bool UseChameleon(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        SetChameleon(ent, !ent.Comp.Toggled.Contains(ChameleonPower));
        return true;
    }

    private void SetChameleon(Entity<ImperialChangelingComponent> ent, bool active)
    {
        if (active == ent.Comp.Toggled.Contains(ChameleonPower))
            return;

        if (active)
        {
            ent.Comp.Toggled.Add(ChameleonPower);
            EnsureComp<StealthComponent>(ent);
            EnsureComp<StealthOnMoveComponent>(ent);
            Popup(ent, "imperial-changeling-chameleon-on");
        }
        else
        {
            ent.Comp.Toggled.Remove(ChameleonPower);
            if (!ent.Comp.Toggled.Contains(DarknessPower))
                RemComp<StealthComponent>(ent);
            RemComp<StealthOnMoveComponent>(ent);
            Popup(ent, "imperial-changeling-chameleon-off");
        }

        SetPowerToggled(ent, ChameleonPower, active);

        Dirty(ent);
    }

    /// <summary>darkness_adaptation: включение стоит 15 химикатов, выключение бесплатно.</summary>
    private bool UseDarkness(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        SetDarkness(ent, !ent.Comp.Toggled.Contains(DarknessPower));
        return true;
    }

    private void SetDarkness(Entity<ImperialChangelingComponent> ent, bool active)
    {
        if (active == ent.Comp.Toggled.Contains(DarknessPower))
            return;

        var uid = ent.Owner;
        if (active)
        {
            ent.Comp.Toggled.Add(DarknessPower);
            EnsureComp<StealthComponent>(uid);
            _darkAlpha.Remove(uid);
            UpdateDarkness(ent);
            EnsureComp<ImperialNightVisionComponent>(uid);
            ent.Comp.RechargeSlowdown += DarknessSlowdown;
            _popup.PopupEntity(Loc.GetString("imperial-changeling-darkness-on-others", ("user", Identity.Entity(uid, EntityManager))),
                uid, Filter.PvsExcept(uid), true, PopupType.MediumCaution);
            Popup(uid, "imperial-changeling-darkness-on-self");
        }
        else
        {
            ent.Comp.Toggled.Remove(DarknessPower);
            _darkAlpha.Remove(uid);
            if (!ent.Comp.Toggled.Contains(ChameleonPower))
                RemComp<StealthComponent>(uid);
            RemComp<ImperialNightVisionComponent>(uid);
            ent.Comp.RechargeSlowdown -= DarknessSlowdown;
            _popup.PopupEntity(Loc.GetString("imperial-changeling-darkness-off-others", ("user", Identity.Entity(uid, EntityManager))),
                uid, Filter.PvsExcept(uid), true, PopupType.MediumCaution);
            Popup(uid, "imperial-changeling-darkness-off-self");
        }

        SetPowerToggled(ent, DarknessPower, active);

        Dirty(ent);
    }

    /// <summary>digitalcamo: ИИ не следит, а люди видят странное.</summary>
    private bool UseDigitalCamo(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        if (ent.Comp.Toggled.Remove(DigitalCamoPower))
        {
            Popup(ent, "imperial-changeling-digicamo-off");
        }
        else
        {
            ent.Comp.Toggled.Add(DigitalCamoPower);
            Popup(ent, "imperial-changeling-digicamo-on");
        }

        SetPowerToggled(ent, DigitalCamoPower, ent.Comp.Toggled.Contains(DigitalCamoPower));

        Dirty(ent);
        return true;
    }

    private void OnExamined(EntityUid uid, ImperialChangelingComponent comp, ExaminedEvent args)
    {
        // /datum/element/digitalcamo
        if (comp.Toggled.Contains(DigitalCamoPower))
            args.PushMarkup(Loc.GetString("imperial-changeling-digicamo-examine", ("user", Identity.Entity(uid, EntityManager))));

        // darkness_adapted/examine_mob()
        if (comp.Toggled.Contains(DarknessPower) && IsShimmering(uid))
            args.PushMarkup(Loc.GetString("imperial-changeling-darkness-examine", ("user", Identity.Entity(uid, EntityManager))));
    }

    #endregion

    #region Биодеградация

    /// <summary>biodegrade: растворить наручники, капканы, шкаф и обжечь держащего.</summary>
    private bool UseBiodegrade(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        var uid = ent.Owner;
        var cuffed = TryComp<CuffableComponent>(uid, out var cuffable) && cuffable.CuffedHandCount > 0;
        var snared = TryComp<EnsnareableComponent>(uid, out var ensnareable) && ensnareable.IsEnsnared;
        var locker = _container.TryGetContainingContainer((uid, null, null), out var container) && HasComp<EntityStorageComponent>(container.Owner)
            ? container.Owner
            : (EntityUid?) null;
        var puller = TryComp<PullableComponent>(uid, out var pullable) ? pullable.Puller : null;

        if (!cuffed && !snared && locker == null && puller == null)
        {
            Popup(uid, "imperial-changeling-biodegrade-free");
            return false;
        }

        if (locker is { } storage)
        {
            _popup.PopupEntity(Loc.GetString("imperial-changeling-biodegrade-locker", ("locker", storage)), storage, PopupType.MediumCaution);
            // Три такта: лужа greenglow и шипение сварки каждую секунду, на четвёртой шкаф плавится.
            for (var beat = 1; beat <= 3; beat++)
                _pendingBeats.Add((storage, _timing.CurTime + TimeSpan.FromSeconds(beat)));
            _pendingMelt.Add((storage, _timing.CurTime + TimeSpan.FromSeconds(4)));
            return true;
        }

        if (cuffed)
        {
            _popup.PopupEntity(Loc.GetString("imperial-changeling-biodegrade-cuffs", ("user", Identity.Entity(uid, EntityManager))), uid, PopupType.MediumCaution);
            _audio.PlayPvs(Welder, uid);
            foreach (var cuff in cuffable!.Container.ContainedEntities.ToList())
            {
                _cuffs.Uncuff(uid, null, cuff, cuffable);
                if (_container.IsEntityInContainer(cuff))
                    _container.Remove(cuff, cuffable.Container, force: true);
                QueueDel(cuff);
            }
        }

        if (snared)
        {
            foreach (var snare in ensnareable!.Container.ContainedEntities.ToList())
            {
                _ensnare.ForceFree(snare, Comp<EnsnaringComponent>(snare));
                QueueDel(snare);
            }
        }

        if (puller is { } manhandler)
            PunishWithAcid(ent, manhandler);

        return true;
    }

    private readonly List<(EntityUid Storage, TimeSpan At)> _pendingMelt = new();

    private readonly List<(EntityUid Storage, TimeSpan At)> _pendingBeats = new();

    private void UpdateMelt(TimeSpan now)
    {
        for (var i = _pendingBeats.Count - 1; i >= 0; i--)
        {
            var (storage, at) = _pendingBeats[i];
            if (now < at)
                continue;

            _pendingBeats.RemoveAt(i);
            if (TerminatingOrDeleted(storage))
                continue;

            _puddle.TrySpillAt(storage, new Solution("FluorosulfuricAcid", 1), out _, sound: false);
            _audio.PlayPvs(Welder, storage, AudioParams.Default.WithVariation(0.125f));
        }

        for (var i = _pendingMelt.Count - 1; i >= 0; i--)
        {
            var (storage, at) = _pendingMelt[i];
            if (now < at)
                continue;

            _pendingMelt.RemoveAt(i);
            if (TerminatingOrDeleted(storage))
                continue;

            // atom_destruction(ACID): содержимое высыпается.
            if (TryComp<EntityStorageComponent>(storage, out var entityStorage))
                _storage.OpenStorage(storage, entityStorage);
            _puddle.TrySpillAt(storage, new Solution("FluorosulfuricAcid", 5), out _, sound: false);
            QueueDel(storage);
        }
    }

    /// <summary>punish_with_acid(): кислота в лицо тому, кто нас держит.</summary>
    private void PunishWithAcid(Entity<ImperialChangelingComponent> ent, EntityUid manhandler)
    {
        var uid = ent.Owner;
        if (HasComp<ImperialChangelingComponent>(manhandler))
        {
            _popup.PopupEntity(Loc.GetString("imperial-changeling-biodegrade-acid-ling", ("user", Identity.Entity(uid, EntityManager)), ("target", Identity.Entity(manhandler, EntityManager))),
                uid, PopupType.MediumCaution);
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-changeling-biodegrade-acid", ("user", Identity.Entity(uid, EntityManager)), ("target", Identity.Entity(manhandler, EntityManager))),
            uid, PopupType.LargeCaution);
        _damage.TryChangeDamage(manhandler, new DamageSpecifier(_proto.Index<DamageTypePrototype>(new Robust.Shared.Prototypes.ProtoId<DamageTypePrototype>("Caustic")), 18), origin: uid);
        _audio.PlayPvs(AcidSpit, uid);
        _stun.TryUpdateStunDuration(manhandler, TimeSpan.FromSeconds(2));
        _chatSystem.TryEmoteWithChat(manhandler, "Scream", ignoreActionBlocker: true, forceEmote: true);
        _pulling.TryStopPull(uid, Comp<PullableComponent>(uid), manhandler);
    }

    #endregion

    #region Феромоны

    private bool UsePheromone(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        SetPheromone(ent, !ent.Comp.Toggled.Contains(PheromonePower));
        return true;
    }

    private void SetPheromone(Entity<ImperialChangelingComponent> ent, bool active)
    {
        if (active == ent.Comp.Toggled.Contains(PheromonePower))
            return;

        if (active)
        {
            ent.Comp.Toggled.Add(PheromonePower);
            ent.Comp.RechargeSlowdown += PheromoneSlowdown;
            ent.Comp.NextPheromoneTick = _timing.CurTime;
            Popup(ent, "imperial-changeling-pheromone-on");
        }
        else
        {
            ent.Comp.Toggled.Remove(PheromonePower);
            ent.Comp.RechargeSlowdown -= PheromoneSlowdown;
            ent.Comp.PheromoneState = null;
            ClearPheromoneAlert(ent);
            Popup(ent, "imperial-changeling-pheromone-off");
        }

        SetPowerToggled(ent, PheromonePower, active);

        Dirty(ent);
    }

    /// <summary>scan_for_target() + point_to_target(): случайный генокрад с перевесом в пользу ближних.</summary>
    private void UpdatePheromone(Entity<ImperialChangelingComponent> ent)
    {
        var here = _transform.GetMapCoordinates(ent.Owner);
        var weights = new Dictionary<EntityUid, float>();
        var query = EntityQueryEnumerator<ImperialChangelingComponent, TransformComponent>();
        while (query.MoveNext(out var other, out _, out var xform))
        {
            if (other == ent.Owner || !_mobState.IsAlive(other))
                continue;

            var there = _transform.GetMapCoordinates(other, xform);
            if (there.MapId != here.MapId)
                continue;

            var distance = (there.Position - here.Position).Length();
            if (distance < PheromoneMax)
                weights[other] = PheromoneMax * PheromoneMax - distance * distance;
        }

        string state;
        var dir = Direction.South;
        if (weights.Count == 0)
        {
            state = "pinonnull";
        }
        else
        {
            var target = _random.Pick(weights);
            var vector = _transform.GetMapCoordinates(target).Position - here.Position;
            var dist = vector.Length();
            dir = vector.ToWorldAngle().GetDir();
            state = dist <= PheromoneMin ? "pinondirect"
                : dist <= PheromoneMid ? "pinonclose"
                : dist <= PheromoneFar ? "pinonmedium"
                : "pinonfar";
        }

        ShowPheromoneAlert(ent, state, dir);
        if (ent.Comp.PheromoneState == state && ent.Comp.PheromoneDir == dir)
            return;

        ent.Comp.PheromoneState = state;
        ent.Comp.PheromoneDir = dir;
        Dirty(ent);
    }

    #endregion

    #region Крики

    /// <summary>resonant_shriek: спутанность и дрожь у не-генокрадов в 4 клетках, лампы бьются.</summary>
    private bool UseResonantShriek(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        var uid = ent.Owner;
        _audio.PlayPvs(Screech, uid, AudioParams.Default.WithVolume(6));
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(uid).Coordinates, 4))
        {
            if (mob.Owner == uid || HasComp<ImperialChangelingComponent>(mob) || !_interaction.InRangeUnobstructed(uid, mob.Owner, 4.5f))
                continue;

            if (HasComp<BorgChassisComponent>(mob))
            {
                _stun.TryUpdateParalyzeDuration(mob, TimeSpan.FromSeconds(_random.Next(10, 21)));
                continue;
            }

            // soundbang_act(deafen_pwr = 1 MINUTES)
            ApplyDeafness(mob, TimeSpan.FromMinutes(1));
            _drunk.TryApplyDrunkenness(mob, TimeSpan.FromSeconds(25));
            _jitter.DoJitter(mob, TimeSpan.FromSeconds(100), false);
        }

        BreakLights(uid, 4);
        return true;
    }

    /// <summary>dissonant_shriek: ЭМИ (2/5) и лампы в 5 клетках.</summary>
    private bool UseTechnoShriek(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        _emp.EmpPulse(Transform(ent).Coordinates, 5, 50000, TimeSpan.FromSeconds(60), ent.Owner);
        BreakLights(ent.Owner, 5);
        return true;
    }

    private void BreakLights(EntityUid uid, float range)
    {
        foreach (var light in _lookup.GetEntitiesInRange<PoweredLightComponent>(Transform(uid).Coordinates, range))
        {
            _lights.TryDestroyBulb(light.Owner, light.Comp);
        }
    }

    #endregion

    #region Пауки, мышцы, голос

    /// <summary>spiders: кладка яиц с ролью паука для призраков.</summary>
    private bool UseSpiders(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        if (_container.IsEntityInContainer(ent.Owner))
        {
            Popup(ent, "imperial-changeling-spiders-space");
            return false;
        }

        Spawn(SpiderEggs, Transform(ent).Coordinates);
        return true;
    }

    private bool UseMuscles(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        SetMuscles(ent, !ent.Comp.Toggled.Contains(MusclesPower));
        return true;
    }

    private void SetMuscles(Entity<ImperialChangelingComponent> ent, bool active, bool quiet = false)
    {
        if (active == ent.Comp.Toggled.Contains(MusclesPower))
            return;

        var uid = ent.Owner;
        if (active)
        {
            ent.Comp.Toggled.Add(MusclesPower);
            ent.Comp.NextMuscleTick = _timing.CurTime + TimeSpan.FromSeconds(4);
            Popup(uid, "imperial-changeling-muscles-on");
        }
        else
        {
            ent.Comp.Toggled.Remove(MusclesPower);
            ent.Comp.NextMuscleTick = _timing.CurTime + TimeSpan.FromSeconds(2);
            if (!quiet)
            {
                Popup(uid, "imperial-changeling-muscles-off");
                if (ent.Comp.MuscleStacks >= 10)
                {
                    Popup(uid, "imperial-changeling-muscles-collapse");
                    _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(6));
                    _chatSystem.TryEmoteWithChat(uid, "Gasp", ignoreActionBlocker: true, forceEmote: true);
                }
            }
        }

        SetPowerToggled(ent, MusclesPower, active);
        Dirty(ent);
        _speed.RefreshMovementSpeedModifiers(uid);
    }

    private void OnRefreshSpeed(Entity<ImperialChangelingComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Toggled.Contains(MusclesPower))
            args.ModifySpeed(MusclesSpeed, MusclesSpeed);
    }

    /// <summary>mimicvoice: голос выбранного имени, пока замедлена выработка химикатов.</summary>
    private bool UseMimicVoice(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        if (ent.Comp.MimicVoice != null)
        {
            SetMimic(ent, null);
            return true;
        }

        if (!TryComp<ActorComponent>(ent, out var actor))
            return false;

        ChooseMimicVoice(ent, actor.PlayerSession);
        return false;
    }

    private void SetMimic(Entity<ImperialChangelingComponent> ent, string? voice)
    {
        if (ent.Comp.MimicVoice == voice)
            return;

        if (ent.Comp.MimicVoice != null)
            ent.Comp.RechargeSlowdown = Math.Max(0, ent.Comp.RechargeSlowdown - MimicSlowdown);

        ent.Comp.MimicVoice = voice;
        SetPowerToggled(ent, MimicVoicePower, voice != null);
        if (voice == null)
        {
            Popup(ent, "imperial-changeling-mimic-off");
            return;
        }

        ent.Comp.RechargeSlowdown += MimicSlowdown;
        Popup(ent, "imperial-changeling-mimic-on", ("name", voice));
    }

    #endregion

    #region Дефиб-хват

    /// <summary>defib_grasp: дефибриллятор будит нас из стазиса, а дефибрилятор получает по рукам.</summary>
    private void OnDefibZap(Entity<ImperialChangelingComponent> ent, ref TargetBeforeDefibrillatorZapsEvent args)
    {
        if (args.Cancelled || !ent.Comp.Purchased.Contains(DefibGraspPower))
            return;

        if (!_mobState.IsDead(ent) && !ent.Comp.FakeDeath)
            return;

        args.Cancel();
        var user = args.EntityUsingDefib;

        if (HasComp<BorgChassisComponent>(user))
        {
            _stun.TryUpdateParalyzeDuration(user, TimeSpan.FromSeconds(2));
            _popup.PopupEntity(Loc.GetString("imperial-changeling-defib-borg", ("user", Identity.Entity(ent, EntityManager))), user, user, PopupType.LargeCaution);
        }
        else
        {
            _stun.TryUpdateStunDuration(user, TimeSpan.FromSeconds(4));
            var removedArms = TearOffArms(user);
            if (removedArms > 0)
            {
                // OH GOOD HEAVENS
                _jitter.DoJitter(user, TimeSpan.FromMinutes(3), true);
                _drunk.TryApplyDrunkenness(user, TimeSpan.FromMinutes(1));
                _stutter.DoStutter(user, TimeSpan.FromMinutes(1), true);
                _chatSystem.TryEmoteWithChat(user, "Scream", ignoreActionBlocker: true, forceEmote: true);

                var filter = Filter.Pvs(ent).RemoveWhereAttachedEntity(e => e == ent.Owner || e == user);
                _popup.PopupEntity(Loc.GetString("imperial-changeling-defib-arms-others", ("user", Identity.Entity(ent, EntityManager)), ("target", Identity.Entity(user, EntityManager)), ("count", removedArms)),
                    ent, filter, true, PopupType.LargeCaution);
                _popup.PopupEntity(Loc.GetString("imperial-changeling-defib-arms-target", ("user", Identity.Entity(ent, EntityManager)), ("count", removedArms)), user, user, PopupType.LargeCaution);
                Popup(ent, "imperial-changeling-defib-arms-self", ("target", Identity.Entity(user, EntityManager)), ("count", removedArms));
                FinishDefibGrasp(ent, args.Defib);
                return;
            }
        }

        _popup.PopupEntity(Loc.GetString("imperial-changeling-defib-others", ("user", Identity.Entity(ent, EntityManager))),
            ent, Filter.PvsExcept(ent), true, PopupType.LargeCaution);
        Popup(ent, "imperial-changeling-defib-self");
        FinishDefibGrasp(ent, args.Defib);
    }

    /// <summary>defib_grasp/activate_effect(): полное лечение, крик и звуки дефибриллятора.</summary>
    private void FinishDefibGrasp(Entity<ImperialChangelingComponent> ent, EntityUid defib)
    {
        if (ent.Comp.FakeDeath)
        {
            ent.Comp.FakeDeath = false;
            ent.Comp.ReviveReady = false;
            ent.Comp.ReviveReadyAt = null;
            SetFakeDeathIcon(ent, "fake_death");
        }

        _rejuvenate.PerformRejuvenate(ent.Owner);
        _mobState.UpdateMobState(ent.Owner);
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(20), true);
        _chatSystem.TryEmoteWithChat(ent, "Scream", ignoreActionBlocker: true, forceEmote: true);
        _audio.PlayPvs(DemonConsume, ent);
        _audio.PlayPvs(BodyFall, defib);
        _audio.PlayPvs(DefibZap, defib);
        _audio.PlayPvs(DefibSuccess, defib);
        Dirty(ent);
    }

    #endregion

    #region Пустота

    /// <summary>void_adaption: холод и вакуум не вредят, дышать не нужно.</summary>
    private void GrantVoid(Entity<ImperialChangelingComponent> ent)
    {
        var uid = ent.Owner;
        EnsureComp<PressureImmunityComponent>(uid);
        if (TryComp<TemperatureDamageComponent>(uid, out var temp))
        {
            _oldColdThreshold[uid] = temp.ColdDamageThreshold;
            temp.ColdDamageThreshold = 0;
        }

        if (RemComp<RespiratorComponent>(uid))
            _removedRespirator.Add(uid);
    }

    private void RemoveVoid(Entity<ImperialChangelingComponent> ent)
    {
        var uid = ent.Owner;
        RemComp<PressureImmunityComponent>(uid);
        if (_oldColdThreshold.Remove(uid, out var cold) && TryComp<TemperatureDamageComponent>(uid, out var temp))
            temp.ColdDamageThreshold = cold;

        if (_removedRespirator.Remove(uid) && MetaData(uid).EntityPrototype is { } proto
            && proto.Components.TryGetValue(_factory.GetComponentName<RespiratorComponent>(), out var respirator))
        {
            EntityManager.AddComponents(uid, new ComponentRegistry { [_factory.GetComponentName<RespiratorComponent>()] = respirator });
        }

        if (ent.Comp.VoidActive)
        {
            ent.Comp.VoidActive = false;
            ent.Comp.RechargeSlowdown -= VoidSlowdown;
            Popup(uid, "imperial-changeling-void-removed");
        }
    }

    /// <summary>check_environment(): опасный холод или вакуум замедляют выработку химикатов.</summary>
    private void UpdateVoid(Entity<ImperialChangelingComponent> ent)
    {
        var uid = ent.Owner;
        string? reason = null;
        if (_atmos.GetContainingMixture(uid) is { } air)
        {
            if (air.Pressure < Atmospherics.HazardLowPressure)
                reason = "imperial-changeling-void-vacuum";
            else if (air.Temperature < 260f)
                reason = "imperial-changeling-void-cold";
        }
        else
        {
            reason = "imperial-changeling-void-vacuum";
        }

        var active = reason != null;
        if (active == ent.Comp.VoidActive)
            return;

        ent.Comp.VoidActive = active;
        if (active)
        {
            ent.Comp.RechargeSlowdown += VoidSlowdown;
            Popup(uid, "imperial-changeling-void-on", ("reason", Loc.GetString(reason!)));
        }
        else
        {
            ent.Comp.RechargeSlowdown -= VoidSlowdown;
            Popup(uid, "imperial-changeling-void-off");
        }
    }

    #endregion
}
