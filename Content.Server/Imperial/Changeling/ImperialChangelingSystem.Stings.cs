using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Forensics.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Changeling;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffect;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Changeling;

/// <summary>
/// Жала TG (tiny_prick.dm): кнопка взводит жало, укол — Alt+Клик по цели в радиусе sting_range.
/// </summary>
public sealed partial class ImperialChangelingSystem
{
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly Content.Shared.StatusEffectNew.StatusEffectsSystem _statusEffectsNew = default!;
    [Dependency] private readonly BlindableSystem _blindable = default!;

    public const string TransformationSting = "ImperialChangelingStingTransformation";
    public const string FalseArmbladeSting = "ImperialChangelingStingFalseArmblade";
    public const string ExtractDnaSting = "ImperialChangelingStingExtractDna";
    public const string MuteSting = "ImperialChangelingStingMute";
    public const string BlindSting = "ImperialChangelingStingBlind";
    public const string LsdSting = "ImperialChangelingStingLsd";
    public const string CryoSting = "ImperialChangelingStingCryo";

    private static readonly EntProtoId FalseBlade = "ImperialChangelingArmBladeFalse";
    private static readonly EntProtoId RainbowEffect = "StatusEffectSeeingRainbow";
    private static readonly SoundSpecifier BlobAttack = new SoundPathSpecifier("/Audio/Imperial/blob/blobattack.ogg", AudioParams.Default.WithVolume(-4));

    /// <summary>transformation/sting_duration.</summary>
    private static readonly TimeSpan TransformStingDuration = TimeSpan.FromMinutes(8);

    /// <summary>false_armblade: addtimer(remove_fake, 1 MINUTES).</summary>
    private static readonly TimeSpan FalseBladeDuration = TimeSpan.FromMinutes(1);

    /// <summary>Жало: доп. проверка can_sting() и sting_action().</summary>
    private readonly Dictionary<string, (Func<Entity<ImperialChangelingComponent>, EntityUid, bool> Can, Func<Entity<ImperialChangelingComponent>, EntityUid, bool> Act)> _stings = new();

    /// <summary>lsd/hallucination_time: когда начнутся галлюцинации.</summary>
    private readonly List<(EntityUid Target, TimeSpan At)> _pendingLsd = new();

    private void InitializeStings()
    {
        _stings[TransformationSting] = (CanTransformationSting, TransformationStingAction);
        _stings[FalseArmbladeSting] = (CanDnaSting, FalseArmbladeAction);
        _stings[ExtractDnaSting] = ((ent, target) => CanAbsorbDna(ent, target, verbose: true), ExtractDnaAction);
        _stings[MuteSting] = ((_, _) => true, MuteAction);
        _stings[BlindSting] = ((_, _) => true, BlindAction);
        _stings[LsdSting] = ((_, _) => true, LsdAction);
        _stings[CryoSting] = ((_, _) => true, CryoAction);

        foreach (var sting in _stings.Keys)
        {
            _powers[sting] = (ent, _) => ToggleSting(ent, sting);
            _powerRemoved[sting] = ent =>
            {
                if (ent.Comp.ChosenSting == sting)
                    UnsetSting(ent);
            };
        }

        SubscribeLocalEvent<MobStateComponent, GetVerbsEvent<AlternativeVerb>>(OnStingVerbs);
        SubscribeLocalEvent<ImperialChangelingComponent, BoundUIClosedEvent>(OnUiClosed);

        // on_click_sting(): COMSIG_MOB_MIDDLECLICKON (Alt+Клик — через глагол).
        CommandBinds.Builder
            .Bind(Content.Shared.Input.ContentKeyFunctions.MouseMiddle, new Robust.Shared.Input.Binding.PointerInputCmdHandler(OnMiddleClick))
            .Register<ImperialChangelingSystem>();
    }

    private void UpdateStings(TimeSpan now)
    {
        for (var i = _pendingLsd.Count - 1; i >= 0; i--)
        {
            var (target, at) = _pendingLsd[i];
            if (now < at)
                continue;

            _pendingLsd.RemoveAt(i);
            if (!TerminatingOrDeleted(target))
                _statusEffectsNew.TryAddStatusEffectDuration(target, RainbowEffect, TimeSpan.FromSeconds(180));
        }

        var stung = EntityQueryEnumerator<ImperialChangelingStungTransformComponent>();
        while (stung.MoveNext(out var uid, out var comp))
        {
            // Таймер стоит, пока жертва мертва.
            if (comp.Remaining is not { } remaining || _mobState.IsDead(uid))
                continue;

            remaining -= TimeSpan.FromSeconds(1);
            comp.Remaining = remaining;
            if (remaining <= TimeSpan.Zero)
                RevertStungTransform((uid, comp));
        }

        var blades = EntityQueryEnumerator<ImperialChangelingFalseBladeComponent>();
        while (blades.MoveNext(out var uid, out var blade))
        {
            if (now < blade.RemoveAt)
                continue;

            // remove_fake()
            if (blade.Holder is { } holder && !TerminatingOrDeleted(holder))
            {
                _audio.PlayPvs(BlobAttack, holder);
                _popup.PopupEntity(Loc.GetString("imperial-changeling-false-blade-remove-others", ("target", Identity.Entity(holder, EntityManager))),
                    holder, Filter.PvsExcept(holder), true, PopupType.MediumCaution);
                _popup.PopupEntity(Loc.GetString("imperial-changeling-false-blade-remove-self"), holder, holder);
            }

            QueueDel(uid);
        }
    }

    #region Выбор жала

    /// <summary>sting/Trigger(): нет жала — взвести это, есть — убрать.</summary>
    private bool ToggleSting(Entity<ImperialChangelingComponent> ent, string sting)
    {
        if (ent.Comp.ChosenSting != null || ent.Comp.SelectingStingDna)
        {
            UnsetSting(ent);
            return false;
        }

        if (sting == TransformationSting)
        {
            // transformation/set_sting(): сначала выбрать ДНК.
            ent.Comp.StingDna = null;
            ent.Comp.SelectingStingDna = true;
            Dirty(ent);
            _ui.OpenUi(ent.Owner, ImperialChangelingUiKey.Transform, ent.Owner);
            return false;
        }

        SetSting(ent, sting);
        return false;
    }

    private void SetSting(Entity<ImperialChangelingComponent> ent, string sting)
    {
        SetPowerToggled(ent, sting, true);
        Popup(ent, "imperial-changeling-sting-set");
        ent.Comp.ChosenSting = sting;
        Dirty(ent);
    }

    private void UnsetSting(Entity<ImperialChangelingComponent> ent)
    {
        if (ent.Comp.ChosenSting is { } previous)
            SetPowerToggled(ent, previous, false);
        Popup(ent, "imperial-changeling-sting-unset");
        ent.Comp.ChosenSting = null;
        ent.Comp.SelectingStingDna = false;
        ent.Comp.StingDna = null;
        Dirty(ent);
    }

    /// <summary>Выбор ДНК для трансформирующего жала вместо превращения.</summary>
    private bool TrySelectStingDna(Entity<ImperialChangelingComponent> ent, EntityUid profile)
    {
        if (!ent.Comp.SelectingStingDna)
            return false;

        ent.Comp.SelectingStingDna = false;
        ent.Comp.StingDna = profile;
        SetSting(ent, TransformationSting);
        return true;
    }

    private void OnUiClosed(Entity<ImperialChangelingComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!Equals(args.UiKey, ImperialChangelingUiKey.Transform) || !ent.Comp.SelectingStingDna)
            return;

        ent.Comp.SelectingStingDna = false;
        Dirty(ent);
    }

    #endregion

    #region Укол

    private bool OnMiddleClick(ICommonSession? session, Robust.Shared.Map.EntityCoordinates coords, EntityUid clicked)
    {
        if (session?.AttachedEntity is not { } user || !TryComp<ImperialChangelingComponent>(user, out var ling) || ling.ChosenSting == null)
            return false;

        if (clicked == user || !HasComp<MobStateComponent>(clicked))
            return false;

        TrySting((user, ling), clicked);
        return true;
    }


    /// <summary>on_click_sting(): Alt+Клик по живому существу.</summary>
    private void OnStingVerbs(Entity<MobStateComponent> target, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (args.User == target.Owner || !TryComp<ImperialChangelingComponent>(args.User, out var ling) || ling.ChosenSting is not { } sting)
            return;

        if (!_proto.TryIndex<ImperialChangelingPowerPrototype>(sting, out var power))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-changeling-sting-verb", ("sting", Loc.GetString(power.Name))),
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/Changeling/actions.rsi"), power.Icon),
            Priority = 100,
            Act = () => TrySting((user, ling), target.Owner),
        });
    }

    /// <summary>sting/try_to_sting() → can_sting() → sting_action().</summary>
    public bool TrySting(Entity<ImperialChangelingComponent> ent, EntityUid target)
    {
        if (ent.Comp.ChosenSting is not { } sting || !_stings.TryGetValue(sting, out var handlers)
            || !_proto.TryIndex<ImperialChangelingPowerPrototype>(sting, out var power))
            return false;

        var cost = GetChemicalCost(ent, power);
        if (!CanUsePower(ent, power, cost))
            return false;

        if (power.DisabledByFire && TryComp<Content.Shared.Atmos.Components.FlammableComponent>(ent, out var flammable) && flammable.OnFire)
        {
            Popup(ent, "imperial-changeling-on-fire");
            return false;
        }

        // iscarbon(target), isturf(user.loc), путь не длиннее sting_range.
        if (!HasComp<BloodstreamComponent>(target) || _container.IsEntityInContainer(ent.Owner))
            return false;

        if (!_interaction.InRangeUnobstructed(ent.Owner, target, ent.Comp.StingRange + 0.5f))
            return false;

        // Генокрад чувствует укол, а жало обходится вдвое дороже.
        if (HasComp<ImperialChangelingComponent>(target))
        {
            StingFeedback(ent, target);
            AdjustChemicals(ent, -cost);
        }

        if (!handlers.Can(ent, target) || !handlers.Act(ent, target))
            return false;

        StingFeedback(ent, target);
        AdjustChemicals(ent, -cost);
        return true;
    }

    /// <summary>sting_feedback().</summary>
    private void StingFeedback(Entity<ImperialChangelingComponent> ent, EntityUid target)
    {
        _popup.PopupEntity(Loc.GetString("imperial-changeling-sting-feedback", ("target", Identity.Entity(target, EntityManager))), ent, ent);
        if (HasComp<ImperialChangelingComponent>(target))
            _popup.PopupEntity(Loc.GetString("imperial-changeling-sting-feel"), target, target, PopupType.SmallCaution);
    }

    private bool CanDnaSting(Entity<ImperialChangelingComponent> ent, EntityUid target)
    {
        if (HasComp<ImperialChangelingAbsorbedComponent>(target) || !TryComp<DnaComponent>(target, out var dna) || dna.DNA == null)
        {
            Popup(ent, "imperial-changeling-sting-dna-incompatible");
            return false;
        }

        return true;
    }

    #endregion

    #region Жала

    private bool CanTransformationSting(Entity<ImperialChangelingComponent> ent, EntityUid target)
    {
        if (!CanDnaSting(ent, target) || !HasComp<Content.Shared.Humanoid.HumanoidProfileComponent>(target))
            return false;

        if (HasComp<ImperialChangelingStungTransformComponent>(target))
        {
            Popup(ent, "imperial-changeling-sting-already-transformed");
            return false;
        }

        if (ent.Comp.StingDna is not { } dna || !ent.Comp.Profiles.Contains(dna))
            return false;

        return true;
    }

    /// <summary>temporary_transformation/trans_sting: 8 минут, у низших форм — навсегда.</summary>
    private bool TransformationStingAction(Entity<ImperialChangelingComponent> ent, EntityUid target)
    {
        var profile = ent.Comp.StingDna!.Value;
        var permanent = IsLesserForm(target);
        var original = _identity.CloneToPausedMap(TransformSettings, target);

        var stung = EnsureComp<ImperialChangelingStungTransformComponent>(target);
        stung.Original = original;
        stung.Remaining = permanent ? null : TransformStingDuration;
        ApplyProfile(target, profile);

        Popup(ent, permanent ? "imperial-changeling-sting-transform-permanent" : "imperial-changeling-sting-transform",
            ("target", Identity.Entity(target, EntityManager)), ("name", Name(profile)));
        return true;
    }

    private void RevertStungTransform(Entity<ImperialChangelingStungTransformComponent> ent)
    {
        if (ent.Comp.Original is { } original && !TerminatingOrDeleted(original))
        {
            ApplyProfile(ent.Owner, original);
            QueueDel(original);
            _popup.PopupEntity(Loc.GetString("imperial-changeling-sting-transform-revert"), ent.Owner, ent.Owner);
        }

        RemComp<ImperialChangelingStungTransformComponent>(ent);
    }

    /// <summary>Облик, ДНК и имя профиля на существо (без плоти-одежды).</summary>
    private void ApplyProfile(EntityUid target, EntityUid profile)
    {
        _visualBody.CopyAppearanceFrom(profile, target);
        _cloning.CloneComponents(profile, target, TransformSettings);
        _metaData.SetEntityName(target, Name(profile), raiseEvents: false);
        _identitySystem.QueueIdentityUpdate(target);
    }

    /// <summary>false_armblade/sting_action().</summary>
    private bool FalseArmbladeAction(Entity<ImperialChangelingComponent> ent, EntityUid target)
    {
        if (_hands.GetActiveHand(target) is not { } hand)
            return false;

        if (_hands.GetActiveItem(target) is { } held && !_hands.TryDrop(target, held))
        {
            Popup(ent, "imperial-changeling-sting-false-blade-held", ("item", held));
            return false;
        }

        var blade = Spawn(FalseBlade, Transform(target).Coordinates);
        if (!_hands.TryPickup(target, blade, hand))
        {
            QueueDel(blade);
            return false;
        }

        var comp = EnsureComp<ImperialChangelingFalseBladeComponent>(blade);
        comp.Holder = target;
        comp.RemoveAt = _timing.CurTime + FalseBladeDuration;

        _popup.PopupEntity(Loc.GetString("imperial-changeling-false-blade-others", ("target", Identity.Entity(target, EntityManager))),
            target, Filter.PvsExcept(target), true, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("imperial-changeling-false-blade-self"), target, target, PopupType.LargeCaution);
        _audio.PlayPvs(BlobAttack, target);
        return true;
    }

    /// <summary>extract_dna/sting_action().</summary>
    private bool ExtractDnaAction(Entity<ImperialChangelingComponent> ent, EntityUid target)
    {
        if (TryComp<DnaComponent>(target, out var dna) && !HasProfileWithDna(ent, dna.DNA))
            AddNewProfile(ent, target);

        return true;
    }

    /// <summary>mute: adjust_silence(1 MINUTES).</summary>
    private bool MuteAction(Entity<ImperialChangelingComponent> ent, EntityUid target)
    {
        _statusEffects.TryAddStatusEffect<MutedComponent>(target, "Muted", TimeSpan.FromMinutes(1), false);
        return true;
    }

    /// <summary>blind: урон глазам 80%, слепота 40 секунд (размытие даёт сам урон глаз).</summary>
    private bool BlindAction(Entity<ImperialChangelingComponent> ent, EntityUid target)
    {
        if (!TryComp<BlindableComponent>(target, out var blindable))
        {
            Popup(ent, "imperial-changeling-sting-no-eyes");
            return false;
        }

        _popup.PopupEntity(Loc.GetString("imperial-changeling-sting-blind-target"), target, target, PopupType.LargeCaution);
        _blindable.AdjustEyeDamage((target, blindable), (int) MathF.Round(blindable.MaxDamage * 0.8f));
        _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(target, TemporaryBlindnessSystem.BlindingStatusEffect,
            TimeSpan.FromSeconds(40), true);
        return true;
    }

    /// <summary>lsd: галлюцинации через 30–60 секунд.</summary>
    private bool LsdAction(Entity<ImperialChangelingComponent> ent, EntityUid target)
    {
        _pendingLsd.Add((target, _timing.CurTime + TimeSpan.FromSeconds(_random.Next(30, 61))));
        return true;
    }

    /// <summary>cryo: 30 ед. морозного масла.</summary>
    private bool CryoAction(Entity<ImperialChangelingComponent> ent, EntityUid target)
    {
        _bloodstream.TryAddToBloodstream(target, new Solution("FrostOil", 30));
        return true;
    }

    #endregion
}
