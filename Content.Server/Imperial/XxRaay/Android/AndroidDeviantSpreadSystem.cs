using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.XxRaay.Android;
using Content.Shared.Imperial.XxRaay.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Shared.IdentityManagement;

namespace Content.Server.Imperial.XxRaay.Android;

/// <summary>
/// Серверная система, управляющая передачей девиантности от одного андроида к другому
/// </summary>
    public sealed class AndroidDeviantSpreadSystem : EntitySystem
    {
        private static readonly TimeSpan ConversionDuration = TimeSpan.FromSeconds(30);

        [Dependency] private readonly AndroidStressSystem _stress = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly PopupSystem _popup = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly UserInterfaceSystem _ui = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly MobStateSystem _mobState = default!;
        [Dependency] private readonly AndroidMemoryWipeSystem _memoryWipe = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AndroidStressComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<AndroidStressComponent, AndroidDeviantConsentChoiceMessage>(OnDeviantConsentChoice);
        SubscribeLocalEvent<AndroidDeviantConsentConversionComponent, AndroidDeviantSpreadDoAfterEvent>(OnSpreadDoAfter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var denyQuery = EntityQueryEnumerator<AndroidDeviantConsentDenyCooldownComponent>();
        while (denyQuery.MoveNext(out var uid, out var denyCooldown))
        {
            if (denyCooldown.DenyEndTime == TimeSpan.Zero || now < denyCooldown.DenyEndTime)
                continue;

            RemCompDeferred<AndroidDeviantConsentDenyCooldownComponent>(uid);
        }

        var consentQuery = EntityQueryEnumerator<AndroidDeviantConsentConversionComponent>();
        while (consentQuery.MoveNext(out var uid, out var consent))
        {
            if (consent.Converter is not { } converter ||
                consent.Target is not { } target)
            {
                RemCompDeferred<AndroidDeviantConsentConversionComponent>(uid);
                continue;
            }

            if (consent.RequestStartTime is { } start &&
                now - start > consent.ResponseTime)
            {
                CancelConsent(target, converter, Loc.GetString("android-deviant-consent-failed-timeout"));
                continue;
            }

            if (!_transform.InRange(Transform(target).Coordinates,
                    Transform(converter).Coordinates,
                    consent.MaxDistance))
            {
                CancelConsent(target, converter, Loc.GetString("android-deviant-consent-failed-out-of-range"));
            }
        }
    }

    private void OnGetVerbs(Entity<AndroidStressComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var target = (EntityUid) ent;
        var user = args.User;

        if (user == target)
            return;

        if (!_mobState.IsAlive(target) || !_mobState.IsAlive(user))
            return;

        ref var targetStress = ref ent.Comp;

        if (!targetStress.IsDeviant && targetStress.CanBeDeviant)
        {
            if (TryComp<AndroidStressComponent>(user, out var userStress) && userStress.IsDeviant)
            {
                if (!TryComp<AndroidDisguiseComponent>(target, out var disguise) ||
                    disguise.State != AndroidDisguiseState.Human)
                {
                    if (!TryComp<AndroidDeviantConsentDenyCooldownComponent>(target, out var denyComp) ||
                        denyComp.DenyEndTime <= _timing.CurTime)
                    {
                        if (!HasComp<AndroidDeviantConsentConversionComponent>(target))
                        {
                            var spreadVerb = new Verb
                            {
                                Text = Loc.GetString("android-deviant-spread-verb-name"),
                                Message = Loc.GetString("android-deviant-spread-verb-desc"),
                                Act = () => RequestConsentConversion(target, user),
                            };

                            args.Verbs.Add(spreadVerb);
                        }
                    }
                }
            }
        }

        if (TryComp<AndroidRk800Component>(user, out var rk800))
        {
            if (!HasComp<AndroidMemoryWipeInProgressComponent>(target))
            {
                if (!TryComp<AndroidDisguiseComponent>(target, out var disguise) ||
                    disguise.State != AndroidDisguiseState.Human)
                {
                    var now = _timing.CurTime;
                    if (rk800.NextMemoryWipeTime <= now)
                    {
                        var wipeVerb = new Verb
                        {
                            Text = Loc.GetString("android-rk800-memorywipe-verb-name"),
                            Message = Loc.GetString("android-rk800-memorywipe-verb-desc"),
                            Act = () => _memoryWipe.StartMemoryWipe(user, target, rk800)
                        };

                        args.Verbs.Add(wipeVerb);
                    }
                }
            }
        }
    }

    private void RequestConsentConversion(EntityUid target, EntityUid converter)
    {
        if (!Exists(target) || !Exists(converter))
            return;

        if (!TryComp<AndroidStressComponent>(target, out var targetStress) || targetStress.IsDeviant || !targetStress.CanBeDeviant)
            return;

        if (!TryComp<AndroidStressComponent>(converter, out var converterStress) || !converterStress.IsDeviant)
            return;

        if (HasComp<AndroidDeviantConsentConversionComponent>(target))
            return;

        var now = _timing.CurTime;

        if (TryComp<AndroidDeviantConsentDenyCooldownComponent>(target, out var denyCooldown) &&
            denyCooldown.DenyEndTime > now)
        {
            _popup.PopupEntity(
                Loc.GetString("android-deviant-consent-deny-active",
                    ("target", Identity.Entity(target, EntityManager))),
                target,
                converter,
                PopupType.SmallCaution);
            return;
        }

        if (TryComp<ActorComponent>(target, out var actor))
        {
            var consent = EnsureComp<AndroidDeviantConsentConversionComponent>(target);
            consent.Converter = converter;
            consent.Target = target;
            consent.RequestStartTime = now;

            _popup.PopupEntity(
                Loc.GetString("android-deviant-consent-requested",
                    ("target", Identity.Entity(target, EntityManager))),
                converter,
                converter);

            _ui.TryOpenUi(target, AndroidDeviantConsentUiKey.Key, target);
            _ui.SetUiState(target, AndroidDeviantConsentUiKey.Key,
                new AndroidDeviantConsentBuiState(Identity.Name(converter, EntityManager)));
        }
        else
        {
            HandleConsentAccepted(target, converter);
        }
    }

    public void HandleConsentAccepted(EntityUid target, EntityUid converter)
    {
        if (!Exists(target) || !Exists(converter))
            return;

        if (!TryGetActiveConversionPair(target, converter, out _, out _))
            return;

        var consent = EnsureComp<AndroidDeviantConsentConversionComponent>(target);
        consent.Converter = converter;
        consent.Target = target;
        consent.RequestStartTime = null;

        _popup.PopupEntity(
            Loc.GetString("android-deviant-consent-convert-popup",
                ("target", Identity.Entity(target, EntityManager))),
            target,
            Filter.Broadcast(),
            true,
            PopupType.LargeCaution);

        var args = new DoAfterArgs(EntityManager, converter, ConversionDuration,
            new AndroidDeviantSpreadDoAfterEvent(), target, target)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
            DistanceThreshold = consent.MaxDistance,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(args);
    }

    public void HandleConsentDenied(EntityUid target, EntityUid converter)
    {
        if (!Exists(target) || !Exists(converter))
            return;

        var now = _timing.CurTime;

        var deny = EnsureComp<AndroidDeviantConsentDenyCooldownComponent>(target);
        deny.DenyEndTime = now + deny.DenyDuration;

        if (TryComp<AndroidDeviantConsentConversionComponent>(target, out var consent))
        {
            consent.Converter = null;
            consent.Target = null;
            consent.RequestStartTime = null;

            RemCompDeferred<AndroidDeviantConsentConversionComponent>(target);
        }

        _popup.PopupEntity(
            Loc.GetString("android-deviant-consent-denied",
                ("target", Identity.Entity(target, EntityManager))),
            target,
            converter,
            PopupType.SmallCaution);
    }

    private void OnDeviantConsentChoice(EntityUid uid, AndroidStressComponent comp, AndroidDeviantConsentChoiceMessage msg)
    {
        if (!TryComp<AndroidDeviantConsentConversionComponent>(uid, out var consent) ||
            consent.Converter is not { } converter)
        {
            return;
        }

        if (msg.Accepted)
            HandleConsentAccepted(uid, converter);
        else
            HandleConsentDenied(uid, converter);

        _ui.CloseUi(uid, AndroidDeviantConsentUiKey.Key);
    }

    private void OnSpreadDoAfter(EntityUid uid, AndroidDeviantConsentConversionComponent comp, ref AndroidDeviantSpreadDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            RemCompDeferred<AndroidDeviantConsentConversionComponent>(uid);
            return;
        }

        if (args.Handled)
            return;

        if (comp.Converter is not { } converter ||
            comp.Target is not { } target)
        {
            RemCompDeferred<AndroidDeviantConsentConversionComponent>(uid);
            return;
        }

        if (!Exists(converter) || !Exists(target))
        {
            RemCompDeferred<AndroidDeviantConsentConversionComponent>(uid);
            return;
        }

        if (!TryGetActiveConversionPair(target, converter, out _, out _))
        {
            RemCompDeferred<AndroidDeviantConsentConversionComponent>(uid);
            return;
        }

        if (!_transform.InRange(Transform(target).Coordinates,
                Transform(converter).Coordinates,
                comp.MaxDistance))
        {
            CancelConsent(target, converter, Loc.GetString("android-deviant-consent-failed-out-of-range"));
            return;
        }

        if (!TryComp<AndroidStressComponent>(converter, out var converterStress) || !converterStress.IsDeviant ||
            !TryComp<AndroidStressComponent>(target, out var targetStress) || targetStress.IsDeviant)
        {
            RemCompDeferred<AndroidDeviantConsentConversionComponent>(uid);
            return;
        }

        if (TryComp<AndroidDisguiseComponent>(target, out var disguise) &&
            disguise.State == AndroidDisguiseState.Human)
        {
            CancelConsent(target, converter, Loc.GetString("android-deviant-consent-failed-disguised"));
            return;
        }

        _stress.TryMakeDeviant(target);

        RemCompDeferred<AndroidDeviantConsentConversionComponent>(uid);
        args.Handled = true;
    }

    private bool TryGetActiveConversionPair(
        EntityUid target,
        EntityUid converter,
        out AndroidStressComponent? targetStress,
        out AndroidStressComponent? converterStress)
    {
        targetStress = null;
        converterStress = null;

        if (!_mobState.IsAlive(target) || !_mobState.IsAlive(converter))
            return false;

        if (!TryComp(converter, out converterStress) || !converterStress.IsDeviant)
            return false;

        if (!TryComp(target, out targetStress) || targetStress.IsDeviant)
            return false;

        return true;
    }

    private void CancelConsent(EntityUid target, EntityUid converter, string? reason = null)
    {
        if (reason != null)
        {
            _popup.PopupEntity(reason, target, target, PopupType.MediumCaution);
            _popup.PopupEntity(reason, converter, converter, PopupType.MediumCaution);
        }

        if (TryComp<AndroidDeviantConsentConversionComponent>(target, out var consent))
        {
            consent.Converter = null;
            consent.Target = null;
            consent.RequestStartTime = null;
            RemCompDeferred<AndroidDeviantConsentConversionComponent>(target);
        }
    }
}

