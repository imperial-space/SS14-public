using Content.Server.Polymorph.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Xenobiology;

/// <summary>Прогресс мутационных токсинов на существе.</summary>
[RegisterComponent]
public sealed partial class SlimeMutationProgressComponent : Component
{
    [ViewVariables]
    public Dictionary<string, int> Cycles = new();
}

/// <summary>disease/transformation/slime.</summary>
[RegisterComponent]
public sealed partial class SlimeTransformationComponent : Component
{
    [ViewVariables]
    public int Stage = 1;

    [ViewVariables]
    public TimeSpan NextTick;
}

/// <summary>
/// reagent/mutationtoxin и disease/transformation/slime.
/// </summary>
public sealed class SlimeToxinSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private readonly SlimeSystem _slime = default!;

    private static readonly ProtoId<PolymorphPrototype> JellyPolymorph = "SlimeMutationJelly";
    private static readonly ProtoId<PolymorphPrototype> SlimePolymorph = "SlimeTransformationSlime";
    private static readonly ProtoId<ReagentPrototype> FrostOil = "FrostOil";
    private static readonly ProtoId<SpeciesPrototype> SlimeSpecies = "SlimePerson";

    /// <summary>stage_prob = 5 за тик в 2 с.</summary>
    private const float StageProb = 0.05f;

    /// <summary>cure_chance = 55.</summary>
    private const float CureChance = 0.55f;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(2);

    public void Mutate(EntityUid uid, SlimeSpeciesMutation effect)
    {
        if (!HasComp<HumanoidProfileComponent>(uid))
            return;

        var progress = EnsureComp<SlimeMutationProgressComponent>(uid);
        var key = effect.Polymorph.Id;
        progress.Cycles[key] = progress.Cycles.GetValueOrDefault(key) + 1;
        if (progress.Cycles[key] < effect.Cycles)
            return;

        progress.Cycles.Remove(key);
        _popup.PopupEntity(Loc.GetString("slime-mutation-toxin-transform"), uid, uid, PopupType.LargeCaution);
        _polymorph.PolymorphEntity(uid, effect.Polymorph);
    }

    public void Infect(EntityUid uid)
    {
        if (!HasComp<MobStateComponent>(uid) || HasComp<SlimeComponent>(uid) || HasComp<SlimeTransformationComponent>(uid))
            return;

        var disease = AddComp<SlimeTransformationComponent>(uid);
        disease.NextTick = _timing.CurTime + TickInterval;

        // Слаймолюды сразу на последней стадии.
        if (TryComp<HumanoidProfileComponent>(uid, out var profile) && profile.Species == SlimeSpecies)
            disease.Stage = 5;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<SlimeTransformationComponent>();
        while (query.MoveNext(out var uid, out var disease))
        {
            if (now < disease.NextTick)
                continue;
            disease.NextTick = now + TickInterval;

            if (HasFrostOil(uid) && _random.Prob(CureChance))
            {
                _popup.PopupEntity(Loc.GetString("slime-transformation-cured"), uid, uid);
                RemCompDeferred<SlimeTransformationComponent>(uid);
                continue;
            }

            if (disease.Stage < 5 && _random.Prob(StageProb))
            {
                disease.Stage++;
                if (disease.Stage == 3 && MakeJelly(uid))
                    continue;
            }

            if (_random.Prob(0.05f) || disease.Stage == 5)
                _popup.PopupEntity(Loc.GetString($"slime-transformation-stage-{disease.Stage}"), uid, uid,
                    disease.Stage >= 3 ? PopupType.MediumCaution : PopupType.Small);

            if (disease.Stage == 5)
                TransformToSlime(uid);
        }
    }

    private bool HasFrostOil(EntityUid uid)
    {
        if (!TryComp<BloodstreamComponent>(uid, out var blood)
            || !_solutions.TryGetSolution(uid, blood.BloodSolutionName, out _, out var solution))
            return false;
        return solution.GetTotalPrototypeQuantity(FrostOil) > 0;
    }

    /// <summary>Стадия 3: человек становится слаймолюдом.</summary>
    private bool MakeJelly(EntityUid uid)
    {
        if (!TryComp<HumanoidProfileComponent>(uid, out var profile) || profile.Species == SlimeSpecies)
            return false;
        if (_polymorph.PolymorphEntity(uid, JellyPolymorph) is not { } jelly)
            return false;

        var disease = EnsureComp<SlimeTransformationComponent>(jelly);
        disease.Stage = 3;
        disease.NextTick = _timing.CurTime + TickInterval;
        return true;
    }

    /// <summary>do_disease_transformation(): new_form = mob/living/basic/slime (случайный, если есть игрок).</summary>
    private void TransformToSlime(EntityUid uid)
    {
        RemCompDeferred<SlimeTransformationComponent>(uid);
        if (_polymorph.PolymorphEntity(uid, SlimePolymorph) is not { } slime || !TryComp<SlimeComponent>(slime, out var slimeComp))
            return;

        if (TryComp<MindContainerComponent>(slime, out var mind) && mind.HasMind)
            _slime.SetSlimeType((slime, slimeComp), _slime.RandomSlimeType());
    }
}

public sealed class SlimeSpeciesMutationEffectSystem : EntityEffectSystem<MetaDataComponent, SlimeSpeciesMutation>
{
    [Dependency] private readonly SlimeToxinSystem _toxin = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<SlimeSpeciesMutation> args)
    {
        _toxin.Mutate(entity, args.Effect);
    }
}

public sealed class SlimeTransformationExposureEffectSystem : EntityEffectSystem<MetaDataComponent, SlimeTransformationExposure>
{
    [Dependency] private readonly SlimeToxinSystem _toxin = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<SlimeTransformationExposure> args)
    {
        _toxin.Infect(entity);
    }
}
