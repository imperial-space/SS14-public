using System.Linq;
using Content.Server.Imperial.MartialArts;
using Content.Shared.Administration.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Changeling;
using Content.Shared.Changeling.Components;
using Content.Shared.Changeling.Systems;
using Content.Shared.Cloning;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Forensics.Components;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Changeling;
using Content.Shared.Imperial.MartialArts;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Objectives.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Changeling;

/// <summary>
/// Врождённые способности TG: absorb_dna, transform, regenerate, fakedeath.
/// </summary>
public sealed partial class ImperialChangelingSystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedObjectivesSystem _objectives = default!;
    [Dependency] private readonly Content.Shared.Roles.Jobs.SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly RejuvenateSystem _rejuvenate = default!;
    [Dependency] private readonly SharedVisualBodySystem _visualBody = default!;
    [Dependency] private readonly SharedCloningSystem _cloning = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IdentitySystem _identitySystem = default!;
    [Dependency] private readonly ChangelingFleshClothingSystem _fleshClothing = default!;
    [Dependency] private readonly Content.Shared.Inventory.InventorySystem _inventory = default!;

    public const string AbsorbPower = "ImperialChangelingAbsorbDna";
    public const string TransformPower = "ImperialChangelingTransform";
    public const string RegeneratePower = "ImperialChangelingRegenerate";
    public const string FakeDeathPower = "ImperialChangelingFakeDeath";

    private static readonly ProtoId<CloningSettingsPrototype> TransformSettings = "ChangelingCloningSettings";
    private static readonly ProtoId<DamageTypePrototype> AbsorbDamage = "Piercing";
    private static readonly ProtoId<DamageTypePrototype> DrainDamage = "Bloodloss";

    /// <summary>attempt_absorb(): 3 итерации по 15 секунд.</summary>
    private static readonly TimeSpan AbsorbStageTime = TimeSpan.FromSeconds(15);

    /// <summary>fakedeath_duration.</summary>
    private static readonly TimeSpan FakeDeathDuration = TimeSpan.FromSeconds(40);

    /// <summary>Сколько ждать повторного нажатия стазиса живым (подтверждение tgui_alert).</summary>
    private static readonly TimeSpan FakeDeathConfirmWindow = TimeSpan.FromSeconds(5);

    /// <summary>/datum/looping_sound/changeling_absorb: mid_length = 3 SECONDS.</summary>
    private static readonly TimeSpan AbsorbSoundInterval = TimeSpan.FromSeconds(3);

    private static readonly SoundSpecifier AbsorbSound = new SoundCollectionSpecifier("ImperialChangelingAbsorb", AudioParams.Default.WithVolume(4));

    /// <summary>Плотская одежда по слотам (ванильные хамелеон-предметы генокрада).</summary>
    private static readonly Dictionary<string, EntProtoId> FleshClothing = new()
    {
        ["shoes"] = "ChangelingFleshClothingFeet",
        ["jumpsuit"] = "ChangelingFleshClothingInner",
        ["outerClothing"] = "ChangelingFleshClothingOuter",
        ["gloves"] = "ChangelingFleshClothingGloves",
        ["neck"] = "ChangelingFleshClothingNeck",
        ["mask"] = "ChangelingFleshClothingMask",
        ["eyes"] = "ChangelingFleshClothingEyes",
        ["ears"] = "ChangelingFleshClothingEars",
        ["head"] = "ChangelingFleshClothingHead",
        ["suitstorage"] = "ChangelingFleshClothingSuitStorage",
        ["belt"] = "ChangelingFleshClothingBelt",
        ["back"] = "ChangelingFleshClothingBack",
    };

    private readonly Dictionary<EntityUid, TimeSpan> _fakeDeathConfirm = new();
    private readonly Dictionary<EntityUid, TimeSpan> _absorbSoundAt = new();

    private void InitializeInnate()
    {
        _powers[AbsorbPower] = UseAbsorb;
        _powers[TransformPower] = UseTransform;
        _powers[RegeneratePower] = UseRegenerate;
        _powers[FakeDeathPower] = UseFakeDeath;

        SubscribeLocalEvent<ImperialChangelingComponent, ImperialChangelingAbsorbDoAfterEvent>(OnAbsorbDoAfter);
        SubscribeLocalEvent<ImperialChangelingComponent, ImperialChangelingTransformSelectMessage>(OnTransformSelect);
        SubscribeLocalEvent<ImperialChangelingComponent, ImperialChangelingDropFleshMessage>(OnDropFlesh);
        SubscribeLocalEvent<ImperialChangelingComponent, UpdateMobStateEvent>(OnUpdateMobState, after: new[] { typeof(MobThresholdSystem) });
        SubscribeLocalEvent<ImperialChangelingComponent, ComponentStartup>((uid, _, _) => EnsureGrabStages(uid));
    }

    private float GetChemicalCost(Entity<ImperialChangelingComponent> ent, ImperialChangelingPowerPrototype power)
    {
        // enable_revive(): chemical_cost = 0, пока возрождение готово.
        if (power.ID == FakeDeathPower && ent.Comp.ReviveReady)
            return 0;

        // darkness_adaptation: выключение бесплатно.
        if (power.ID == DarknessPower && ent.Comp.Toggled.Contains(DarknessPower))
            return 0;

        return power.ChemicalCost;
    }

    private void UpdateInnate(Entity<ImperialChangelingComponent> ent, TimeSpan now)
    {
        EnsureGrabStages(ent);

        // ready_to_regenerate()
        if (ent.Comp.FakeDeath && !ent.Comp.ReviveReady && ent.Comp.ReviveReadyAt is { } ready && now >= ready)
        {
            ent.Comp.ReviveReadyAt = null;
            ent.Comp.ReviveReady = true;
            Dirty(ent);
            Popup(ent, "imperial-changeling-fakedeath-ready");
            SetFakeDeathIcon(ent, "revive");
        }

        if (ent.Comp.Absorbing && _absorbSoundAt.TryGetValue(ent.Owner, out var soundAt) && now >= soundAt)
        {
            _absorbSoundAt[ent.Owner] = now + AbsorbSoundInterval;
            _audio.PlayPvs(AbsorbSound, ent.Owner);
        }
    }

    #region Поглощение

    /// <summary>absorb_dna/can_sting().</summary>
    private bool UseAbsorb(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        if (ent.Comp.Absorbing)
        {
            Popup(ent, "imperial-changeling-absorb-busy");
            return false;
        }

        if (!TryComp<PullerComponent>(ent, out var puller) || puller.Pulling is not { } target || !HasComp<MobStateComponent>(target))
        {
            Popup(ent, "imperial-changeling-absorb-grab");
            return false;
        }

        if (!IsStrongGrab(ent, target))
        {
            Popup(ent, "imperial-changeling-absorb-grab-stronger");
            return false;
        }

        if (!CanAbsorbDna(ent, target, verbose: true))
            return false;

        ent.Comp.Absorbing = true;
        StartAbsorbStage(ent, target, 1);
        return true;
    }

    /// <summary>
    /// owner.grab_state > GRAB_NECK: нужен удушающий захват. Стадии захвата TG живут в MartialArtsComponent —
    /// генокрад получает его без стилей (EnsureGrabStages), поэтому усиливает захват как любой человек в TG:
    /// в боевом режиме повторной тягой.
    /// </summary>
    private bool IsStrongGrab(EntityUid user, EntityUid target)
    {
        return TryComp<MartialArtsComponent>(user, out var martial) && martial.GrabTarget == target && martial.GrabState >= MartialGrabState.Kill;
    }

    /// <summary>Стадии захвата (passive → aggressive → neck → kill) для генокрада без боевых искусств.</summary>
    private void EnsureGrabStages(EntityUid uid)
    {
        if (!HasComp<MartialArtsComponent>(uid))
            AddComp<MartialArtsComponent>(uid);
    }

    /// <summary>can_absorb_dna().</summary>
    public bool CanAbsorbDna(Entity<ImperialChangelingComponent> ent, EntityUid target, bool verbose)
    {
        if (ent.Comp.Profiles.Count > 0)
        {
            // Если самый старый профиль — наш нынешний облик, его нельзя вытеснить: надо сначала превратиться.
            var top = ent.Comp.Profiles[0];
            if (SameDna(top, ent.Owner) && ent.Comp.Profiles.Count > ent.Comp.DnaMax)
            {
                if (verbose)
                    Popup(ent, "imperial-changeling-absorb-full");
                return false;
            }
        }

        if (!TryComp<DnaComponent>(target, out var dna) || dna.DNA == null)
        {
            if (verbose)
                Popup(ent, "imperial-changeling-absorb-incompatible", ("target", Identity.Entity(target, EntityManager)));
            return false;
        }

        if (HasProfileWithDna(ent, dna.DNA))
        {
            if (verbose)
                Popup(ent, "imperial-changeling-absorb-have");
            return false;
        }

        if (HasComp<ImperialChangelingAbsorbedComponent>(target))
        {
            if (verbose)
                Popup(ent, "imperial-changeling-absorb-husk", ("target", Identity.Entity(target, EntityManager)));
            return false;
        }

        if (!HasComp<HumanoidProfileComponent>(target) || IsLesserForm(target))
        {
            if (verbose)
                Popup(ent, "imperial-changeling-absorb-lesser");
            return false;
        }

        return true;
    }

    private bool SameDna(EntityUid a, EntityUid b)
    {
        return TryComp<DnaComponent>(a, out var da) && TryComp<DnaComponent>(b, out var db) && da.DNA != null && da.DNA == db.DNA;
    }

    /// <summary>Сообщение в начале итерации и do_after на 15 секунд.</summary>
    private void StartAbsorbStage(Entity<ImperialChangelingComponent> ent, EntityUid target, int stage)
    {
        var user = ent.Owner;
        switch (stage)
        {
            case 1:
                Popup(user, "imperial-changeling-absorb-stage1");
                break;
            case 2:
                _popup.PopupEntity(Loc.GetString("imperial-changeling-absorb-stage2-others", ("user", Identity.Entity(user, EntityManager))),
                    user, Robust.Shared.Player.Filter.PvsExcept(user), true, PopupType.MediumCaution);
                Popup(user, "imperial-changeling-absorb-stage2-self");
                break;
            case 3:
                _popup.PopupEntity(Loc.GetString("imperial-changeling-absorb-stage3-others", ("user", Identity.Entity(user, EntityManager)), ("target", Identity.Entity(target, EntityManager))),
                    user, Robust.Shared.Player.Filter.PvsExcept(user), true, PopupType.LargeCaution);
                Popup(user, "imperial-changeling-absorb-stage3-self", ("target", Identity.Entity(target, EntityManager)));
                _popup.PopupEntity(Loc.GetString("imperial-changeling-absorb-stage3-target"), target, target, PopupType.LargeCaution);
                var damage = new DamageSpecifier(_proto.Index(AbsorbDamage), 40);
                _damage.TryChangeDamage(target, damage, origin: user);
                _absorbSoundAt[user] = _timing.CurTime;
                break;
        }

        var args = new DoAfterArgs(EntityManager, user, AbsorbStageTime, new ImperialChangelingAbsorbDoAfterEvent(stage), user, target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
            NeedHand = false,
            Hidden = true,
            DistanceThreshold = 1.5f,
        };

        if (!_doAfter.TryStartDoAfter(args))
            StopAbsorb(ent);
    }

    private void StopAbsorb(Entity<ImperialChangelingComponent> ent)
    {
        ent.Comp.Absorbing = false;
        _absorbSoundAt.Remove(ent.Owner);
    }

    private void OnAbsorbDoAfter(Entity<ImperialChangelingComponent> ent, ref ImperialChangelingAbsorbDoAfterEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (args.Cancelled || args.Target is not { } target || TerminatingOrDeleted(target))
        {
            Popup(ent, "imperial-changeling-absorb-interrupted");
            StopAbsorb(ent);
            return;
        }

        if (args.Stage < 3)
        {
            StartAbsorbStage(ent, target, args.Stage + 1);
            return;
        }

        StopAbsorb(ent);
        FinishAbsorb(ent, target);
    }

    /// <summary>absorb_dna/sting_action() после трёх итераций.</summary>
    internal void FinishAbsorb(Entity<ImperialChangelingComponent> ent, EntityUid target)
    {
        var user = ent.Owner;
        _popup.PopupEntity(Loc.GetString("imperial-changeling-absorb-done-others", ("user", Identity.Entity(user, EntityManager)), ("target", Identity.Entity(target, EntityManager))),
            user, Robust.Shared.Player.Filter.PvsExcept(user), true, PopupType.LargeCaution);
        Popup(user, "imperial-changeling-absorb-done-self", ("target", Identity.Entity(target, EntityManager)));

        // true_absorbtion: client || mind || last_mind.
        var trueAbsorb = TryComp<MindContainerComponent>(target, out var container) && (container.HasMind || container.Mind != null);
        if (!trueAbsorb)
            Popup(user, "imperial-changeling-absorb-weak", ("target", Identity.Entity(target, EntityManager)));

        if (TryComp<DnaComponent>(target, out var dna) && !HasProfileWithDna(ent, dna.DNA))
        {
            AddNewProfile(ent, target);
            if (trueAbsorb)
                ent.Comp.TrueAbsorbs++;
        }

        // Питание: не больше NUTRITION_LEVEL_WELL_FED.
        if (TryComp<HungerComponent>(user, out var ourHunger) && TryComp<HungerComponent>(target, out var theirHunger))
        {
            var wellFed = ourHunger.Thresholds[HungerThreshold.Okay];
            var current = _hunger.GetHunger(ourHunger);
            if (current < wellFed)
                _hunger.SetHunger(user, Math.Min(current + _hunger.GetHunger(theirHunger), wellFed), ourHunger);
        }

        if (_mind.TryGetMind(target, out var mindId, out var mind))
            AbsorbMemories(ent, target, mindId, mind);

        AdjustChemicals(ent, 10);
        if (trueAbsorb)
            ent.Comp.CanRespec++;

        // death() и Drain(): тело высушено.
        if (_thresholds.TryGetThresholdForState(target, MobState.Dead, out var deadAt) && TryComp<DamageableComponent>(target, out var damageable))
        {
            var missing = deadAt.Value - _damage.GetTotalDamage((target, damageable));
            if (missing > 0)
                _damage.TryChangeDamage(target, new DamageSpecifier(_proto.Index(DrainDamage), missing + 1), true, origin: user);
        }

        _bloodstream.TryModifyBloodLevel(target, -1000);
        EnsureComp<ImperialChangelingAbsorbedComponent>(target);
        Dirty(ent);
        SyncMind(ent);
        UpdateInfoUi(ent);
    }

    /// <summary>absorb_memories(): цели жертвы и, если она генокрад, её сила.</summary>
    private void AbsorbMemories(Entity<ImperialChangelingComponent> ent, EntityUid target, EntityUid mindId, MindComponent mind)
    {
        var name = mind.CharacterName ?? Name(target);

        // stolen_memories: в SS14 нет памяти разума — запоминаем, кем была жертва.
        var job = _jobs.MindTryGetJobName(mindId);
        ent.Comp.Memories.Add(new ImperialChangelingMemory(name, Loc.GetString("imperial-changeling-memory-story", ("name", name), ("job", job))));

        // antag_memory: задачи жертвы, если она была антагонистом.
        var objectives = new List<string>();
        foreach (var objective in mind.Objectives)
        {
            if (_objectives.GetInfo(objective, mindId, mind) is { } info)
                objectives.Add(info.Title);
        }

        var stolen = new System.Text.StringBuilder(ent.Comp.StolenAntagInfo);
        if (objectives.Count > 0)
        {
            stolen.Append(Loc.GetString("imperial-changeling-memory-objectives-header", ("name", name)));
            for (var i = 0; i < objectives.Count; i++)
            {
                stolen.Append(Loc.GetString("imperial-changeling-memory-objective", ("index", i + 1), ("text", objectives[i])));
            }
        }

        stolen.Append(Loc.GetString("imperial-changeling-memory-end", ("name", name)));
        AbsorbRecentSpeech(ent, target, name, stolen);
        ent.Comp.StolenAntagInfo = stolen.ToString();

        if (!TryComp<ImperialChangelingComponent>(target, out var other))
            return;

        // Генокрад поглотил генокрада: половина очков и химикатов, всё его ДНК.
        Popup(ent, "imperial-changeling-absorb-ling", ("target", Identity.Entity(target, EntityManager)));
        var points = other.TotalGeneticPoints / 2;
        ent.Comp.GeneticPoints += points;
        ent.Comp.TotalGeneticPoints += points;
        var chems = MathF.Round(other.MaxChemicals / 2);
        AdjustChemicals(ent, chems);
        ent.Comp.MaxChemicals += chems;
        ent.Comp.AbsorbedCount += other.AbsorbedCount;

        // remove_antag_datum + fallen_changeling: цели остаются на разуме для итогов раунда.
        RemComp<ImperialChangelingComponent>(target);
        EnsureComp<ImperialChangelingFallenComponent>(mindId);
        RaiseLocalEvent(new ImperialChangelingFellEvent(mindId));
    }

    #endregion

    #region Превращение

    /// <summary>transform/sting_action(): сначала выбор в радиальном меню, химикаты списываются после выбора.</summary>
    private bool UseTransform(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        _ui.TryToggleUi(ent.Owner, ImperialChangelingUiKey.Transform, ent.Owner);
        return false;
    }

    private void OnTransformSelect(Entity<ImperialChangelingComponent> ent, ref ImperialChangelingTransformSelectMessage args)
    {
        // Выбор ДНК для жала решаем до закрытия окна: закрытие сбрасывает режим.
        var valid = TryGetEntity(args.Profile, out var profile) && ent.Comp.Profiles.Contains(profile!.Value);
        var stingDna = valid && (TrySelectHumanForm(ent, profile!.Value) || TrySelectStingDna(ent, profile!.Value));
        _ui.CloseUi(ent.Owner, ImperialChangelingUiKey.Transform);
        if (!valid || stingDna)
            return;

        if (!_proto.TryIndex<ImperialChangelingPowerPrototype>(TransformPower, out var power))
            return;

        var cost = GetChemicalCost(ent, power);
        if (!CanUsePower(ent, power, cost))
            return;

        TransformInto(ent, profile!.Value);
        AdjustChemicals(ent, -cost);
    }

    /// <summary>«Сбросить маскировку из плоти»: плоть возвращается в тело.</summary>
    private void OnDropFlesh(Entity<ImperialChangelingComponent> ent, ref ImperialChangelingDropFleshMessage args)
    {
        var selecting = ent.Comp.SelectingStingDna;
        _ui.CloseUi(ent.Owner, ImperialChangelingUiKey.Transform);
        if (selecting)
            return;

        var slots = _inventory.GetSlotEnumerator(ent.Owner);
        while (slots.NextItem(out var item))
        {
            if (HasComp<ChangelingFleshClothingComponent>(item))
                QueueDel(item);
        }
    }

    /// <summary>
    /// /datum/antagonist/changeling/proc/transform(): мгновенно копирует облик, ДНК и плоть-одежду профиля.
    /// </summary>
    public void TransformInto(Entity<ImperialChangelingComponent> ent, EntityUid profile)
    {
        var user = ent.Owner;
        RaiseLocalEvent(user, new BeforeChangelingTransformEvent(profile));
        _fleshClothing.RemoveRedundantFleshClothing(user, profile);

        _visualBody.CopyAppearanceFrom(profile, user);
        _cloning.CloneComponents(profile, user, TransformSettings);
        _metaData.SetEntityName(user, Name(profile), raiseEvents: false);
        _identitySystem.QueueIdentityUpdate(user);

        if (TryComp<ChangelingIdentityComponent>(user, out var identity))
        {
            identity.CurrentIdentity = profile;
            Dirty(user, identity);
        }

        _fleshClothing.SpawnAndTransformClothing(user, profile, FleshClothing);
        RaiseLocalEvent(user, new AfterChangelingTransformEvent(profile));

        ent.Comp.CurrentProfile = profile;
        Dirty(ent);
    }

    #endregion

    #region Регенерация

    /// <summary>regenerate: fully_heal(HEAL_BODY) — кровь, кровотечение и органы, без лечения урона.</summary>
    private bool UseRegenerate(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        Popup(ent, "imperial-changeling-regenerate");
        _bloodstream.TryModifyBleedAmount(ent.Owner, -1000);
        _bloodstream.TryModifyBloodLevel(ent.Owner, 1000);

        // got_limbs_back: звук и крик уже после лечения, чтобы глухой генокрад их услышал.
        if (RegenerateLimbs(ent, LimbCategories))
            LimbsRegrownFeedback(ent, "imperial-changeling-limbs-regrow-others", "imperial-changeling-limbs-regrow-self");
        return true;
    }

    #endregion

    #region Стазис

    /// <summary>fakedeath/sting_action().</summary>
    private bool UseFakeDeath(Entity<ImperialChangelingComponent> ent, EntityUid? _)
    {
        if (ent.Comp.ReviveReady)
        {
            Revive(ent);
            return true;
        }

        // can_enter_stasis()
        if (ent.Comp.FakeDeath)
        {
            Popup(ent, "imperial-changeling-fakedeath-already");
            return false;
        }

        // tgui_alert «Уверены ли мы, что хотим инсценировать собственную смерть?»
        var now = _timing.CurTime;
        if (!_mobState.IsDead(ent) && !Confirm(ent, FakeDeathPower, "imperial-changeling-fakedeath-confirm-title", "imperial-changeling-fakedeath-confirm"))
            return false;

        ent.Comp.FakeDeath = true;
        ent.Comp.ReviveReady = false;
        ent.Comp.ReviveReadyAt = now + FakeDeathDuration;
        Dirty(ent);
        _mobState.UpdateMobState(ent.Owner);
        Popup(ent, "imperial-changeling-fakedeath-start");
        return true;
    }

    /// <summary>fakedeath(): тело выглядит мёртвым, пока идёт стазис.</summary>
    private void OnUpdateMobState(Entity<ImperialChangelingComponent> ent, ref UpdateMobStateEvent args)
    {
        if (ent.Comp.FakeDeath)
            args.State = MobState.Dead;
    }

    /// <summary>revive(): лечит урон, тело и статусы.</summary>
    private void Revive(Entity<ImperialChangelingComponent> ent)
    {
        ent.Comp.FakeDeath = false;
        ent.Comp.ReviveReady = false;
        ent.Comp.ReviveReadyAt = null;
        Dirty(ent);
        SetFakeDeathIcon(ent, "fake_death");

        _rejuvenate.PerformRejuvenate(ent.Owner);
        _mobState.UpdateMobState(ent.Owner);
        Popup(ent, "imperial-changeling-fakedeath-revived");

        // Голова не отрастает (headless changelings are funny), а её в SS14 и не оторвать.
        if (RegenerateLimbs(ent, LimbCategories))
            LimbsRegrownFeedback(ent, "imperial-changeling-limbs-form-others", "imperial-changeling-limbs-regrow-self");
    }

    private void SetFakeDeathIcon(Entity<ImperialChangelingComponent> ent, string state)
    {
        if (ent.Comp.Actions.TryGetValue(FakeDeathPower, out var action))
            _actions.SetIcon(action, new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/Changeling/actions.rsi"), state));
    }

    #endregion
}
