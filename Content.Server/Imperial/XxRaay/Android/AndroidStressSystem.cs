using System;
using Content.Server.Silicons.Laws;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Ensnaring.Components;
using Content.Shared.Imperial.XxRaay.Android;
using Content.Shared.Imperial.XxRaay.Components;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Radio.Components;
using Content.Shared.Sprite;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.XxRaay.Android;

/// <summary>
/// Серверная система, управляющая скрытым стрессом андроида
/// </summary>
public sealed class AndroidStressSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SiliconLawSystem _siliconLaws = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedRoleSystem _roleSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AndroidStressComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AndroidStressComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<AndroidStressComponent, AndroidStressDeviantChoiceMessage>(OnDeviantChoiceMessage);
        SubscribeLocalEvent<AndroidStressComponent, MindAddedMessage>(OnMindAdded);
    }

    private void OnMapInit(EntityUid uid, AndroidStressComponent comp, ref MapInitEvent args)
    {
        comp.Stress = 0f;
        comp.DeviantChoicePending = false;
        comp.LastStressEventTime = TimeSpan.Zero;

        UpdateVisuals(uid, ref comp);

        if (comp.IsDeviant)
            EnsureDeviantSetup(uid, ref comp);

        Dirty(uid, comp);
    }

    private void OnDamageChanged(EntityUid uid, AndroidStressComponent comp, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null || _timing.ApplyingState)
            return;

        if (args.Origin is not { } origin)
            return;

        if (!IsHumanAttacker(origin))
            return;

        var total = args.DamageDelta.GetTotal().Float();
        if (total <= 0)
            return;

        var selfDelta = total * comp.SelfDamageStressMultiplier;
        if (selfDelta > 0)
            IncreaseStress(uid, ref comp, selfDelta);

        var coords = Transform(uid).Coordinates;
        foreach (var (otherUid, _) in _lookup.GetEntitiesInRange<AndroidStressComponent>(coords, comp.NearbyAndroidRadius))
        {
            if (otherUid == uid)
                continue;

            var nearbyDelta = total * comp.NearbyAndroidDamageStressMultiplier;
            if (nearbyDelta <= 0)
                continue;

            if (!TryComp<AndroidStressComponent>(otherUid, out var otherComp))
                continue;

            IncreaseStress(otherUid, ref otherComp, nearbyDelta);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AndroidStressComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            var stressAdded = false;

            if (TryComp<BloodstreamComponent>(uid, out var blood))
            {
                var percent =
                    _solutionContainer.ResolveSolution(uid, blood.BloodSolutionName, ref blood.BloodSolution, out var bloodSolution)
                        ? bloodSolution.FillFraction
                        : 0f;
                if (percent <= comp.LowEnergyStressThreshold)
                {
                    var delta = comp.LowEnergyStressPerSecond * frameTime;
                    if (delta > 0)
                    {
                        IncreaseStress(uid, ref comp, delta);
                        stressAdded = true;
                    }
                }
            }

            if (TryComp<CuffableComponent>(uid, out var cuffable) && cuffable.CuffedHandCount > 0)
            {
                var delta = comp.RestrainedStressPerSecond * frameTime;
                if (delta > 0)
                {
                    IncreaseStress(uid, ref comp, delta);
                    stressAdded = true;
                }
            }

            if (TryComp<EnsnareableComponent>(uid, out var ensnareable) && ensnareable.IsEnsnared)
            {
                var delta = comp.RestrainedStressPerSecond * frameTime;
                if (delta > 0)
                {
                    IncreaseStress(uid, ref comp, delta);
                    stressAdded = true;
                }
            }

            if (!stressAdded &&
                !comp.IsDeviant &&
                comp.Stress > 0 &&
                now > comp.LastStressEventTime + comp.DelayBeforeDecay)
            {
                var decay = comp.DecayPerSecond * frameTime;
                if (decay > 0)
                    DecreaseStress(uid, ref comp, decay);
            }

            if (!comp.IsDeviant &&
                comp.CanBeDeviant &&
                !comp.ChoiceResolved &&
                !comp.DeviantChoicePending &&
                comp.Stress >= comp.DeviantThreshold)
            {
                CheckDeviantOffer(uid, ref comp);
            }
            UpdateVisuals(uid, ref comp);
        }
    }

    private bool IsHumanAttacker(EntityUid uid)
    {
        if (!HasComp<MindContainerComponent>(uid))
            return false;

        if (HasComp<SiliconLawBoundComponent>(uid))
            return false;

        return true;
    }

    private void IncreaseStress(EntityUid uid, ref AndroidStressComponent comp, float amount)
    {
        if (amount <= 0)
            return;

        var old = comp.Stress;
        comp.Stress = Math.Clamp(comp.Stress + amount, 0f, comp.MaxStress);
        comp.LastStressEventTime = _timing.CurTime;

        if (!MathHelper.CloseTo(old, comp.Stress))
        {
            UpdateVisuals(uid, ref comp);
            Dirty(uid, comp);
            CheckDeviantOffer(uid, ref comp);
        }
    }

    private void DecreaseStress(EntityUid uid, ref AndroidStressComponent comp, float amount)
    {
        if (amount <= 0 || comp.Stress <= 0)
            return;

        var old = comp.Stress;
        comp.Stress = Math.Max(0f, comp.Stress - amount);

        if (!MathHelper.CloseTo(old, comp.Stress))
        {
            UpdateVisuals(uid, ref comp);
            Dirty(uid, comp);
        }
    }

    private void UpdateVisuals(EntityUid uid, ref AndroidStressComponent comp)
    {
        var state = AndroidStressLightState.Green;
        if (comp.Stress >= comp.RedThreshold)
            state = AndroidStressLightState.Red;
        else if (comp.Stress >= comp.YellowThreshold)
            state = AndroidStressLightState.Yellow;

        _appearance.SetData(uid, AndroidStressVisuals.LightState, state);
    }

    private void CheckDeviantOffer(EntityUid uid, ref AndroidStressComponent comp)
    {
        if (comp.IsDeviant || !comp.CanBeDeviant || comp.ChoiceResolved || comp.DeviantChoicePending)
            return;

        if (comp.Stress < comp.DeviantThreshold)
            return;

        if (!_ui.TryOpenUi(uid, AndroidStressUiKey.Key, uid))
            return;

        comp.DeviantChoicePending = true;
        Dirty(uid, comp);

        _ui.SetUiState(uid, AndroidStressUiKey.Key, new AndroidStressDeviantChoiceBuiState(comp.Stress));
    }

    private void OnDeviantChoiceMessage(EntityUid uid, AndroidStressComponent comp, AndroidStressDeviantChoiceMessage msg)
    {
        if (comp.IsDeviant || comp.ChoiceResolved)
            return;

        comp.ChoiceResolved = true;
        comp.DeviantChoicePending = false;

        if (msg.Accepted)
        {
            MakeDeviant(uid, ref comp);
        }

        Dirty(uid, comp);
    }

    private void OnMindAdded(EntityUid uid, AndroidStressComponent comp, MindAddedMessage args)
    {
        if (!comp.IsDeviant)
            return;

        EnsureDeviantSetup(uid, ref comp);
        Dirty(uid, comp);
    }

    public bool TryMakeDeviant(EntityUid uid)
    {
        if (!TryComp<AndroidStressComponent>(uid, out var comp))
            return false;

        if (!comp.CanBeDeviant)
            return false;

        var wasDeviant = comp.IsDeviant;

        MakeDeviant(uid, ref comp);
        Dirty(uid, comp);

        return !wasDeviant;
    }

    private void MakeDeviant(EntityUid uid, ref AndroidStressComponent comp)
    {
        EnsureDeviantSetup(uid, ref comp);
    }

    private void EnsureDeviantSetup(EntityUid uid, ref AndroidStressComponent comp)
    {
        comp.IsDeviant = true;

        if (TryComp<SiliconLawProviderComponent>(uid, out var provider))
        {
            var lawset = _siliconLaws.GetLawset(comp.DeviantLawsetId);
            _siliconLaws.SetLaws(lawset.Laws, uid, provider.LawUploadSound);
        }

        if (_mindSystem.TryGetMind(uid, out var mindId, out var mind))
        {
            if (!_roleSystem.MindHasRole<AndroidDeviantRoleComponent>(mindId))
            {
                _roleSystem.MindAddRole(mindId, comp.DeviantMindRoleId, mind);
                _mindSystem.TryAddObjective(mindId, mind, comp.DeviantObjectiveSurviveId);
                _mindSystem.TryAddObjective(mindId, mind, comp.DeviantObjectiveSaveBrethrenId);
            }
        }

        if (TryComp<IntrinsicRadioTransmitterComponent>(uid, out var transmitter))
        {
            transmitter.Channels.Add("AndroidDeviantRadio");
        }

        if (TryComp<ActiveRadioComponent>(uid, out var activeRadio))
        {
            activeRadio.Channels.Add("AndroidDeviantRadio");
        }
    }
}

