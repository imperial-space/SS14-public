using System.Linq;
using Content.Server.Payload.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Imperial.Research.Items;
using Content.Shared.Payload.Components;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Systems;

namespace Content.Server.Imperial.Research.Items;

/// <summary>
/// Корпуса химгранат TG (chem_grenade.dm): ignition_temp нагревает или охлаждает смесь перед смешиванием,
/// adv_release выпускает по unit_spread единиц за срабатывание и взводится снова, пока есть содержимое.
/// </summary>
public sealed class ImperialTgGrenadeCasingSystem : EntitySystem
{
    [Dependency] private readonly PayloadSystem _payload = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ImperialTgGrenadeCasingComponent, TriggerEvent>(OnBeforeTrigger, before: new[] { typeof(PayloadSystem) });
    }

    private void OnBeforeTrigger(Entity<ImperialTgGrenadeCasingComponent> ent, ref TriggerEvent args)
    {
        var comp = ent.Comp;
        var beakers = GetBeakers(ent).ToList();

        // Возвращаем отложенное с прошлого выброса.
        foreach (var (beaker, stash) in comp.Stash)
        {
            if (!Exists(beaker))
                continue;
            if (TryGetSolution(beaker, out var soln, out _))
                _solution.TryAddSolution(soln!.Value, stash);
        }
        comp.Stash.Clear();

        var total = 0f;
        foreach (var beaker in beakers)
        {
            if (TryGetSolution(beaker, out _, out var sol))
                total += sol.Volume.Float();
        }

        if (comp.ReleaseUnits > 0 && total > comp.ReleaseUnits)
        {
            // Оставляем в стаканах только долю unit_spread, остальное откладываем до следующего срабатывания.
            var keep = comp.ReleaseUnits / total;
            foreach (var beaker in beakers)
            {
                if (!TryGetSolution(beaker, out var soln, out var sol))
                    continue;

                var volume = sol.Volume;
                var stash = _solution.SplitSolution(soln!.Value, volume * (1f - keep));
                comp.Stash[beaker] = stash;
            }

            if (TryComp<TimerTriggerComponent>(ent, out _))
                Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(comp.ReleaseDelay), () =>
                {
                    if (!TerminatingOrDeleted(ent) && ent.Comp.Stash.Count > 0)
                        _trigger.ActivateTimerTrigger(ent.Owner);
                });
        }

        if (comp.TemperatureDelta == 0)
            return;

        foreach (var beaker in beakers)
        {
            if (!TryGetSolution(beaker, out var soln, out var solution))
                continue;

            _solution.SetTemperature(soln!.Value, MathF.Max(2.7f, solution.Temperature + comp.TemperatureDelta));
        }
    }

    private IEnumerable<EntityUid> GetBeakers(EntityUid casing)
    {
        foreach (var payload in _payload.GetAllPayloads(casing))
        {
            if (!TryComp<ChemicalPayloadComponent>(payload, out var chem))
                continue;

            if (chem.BeakerSlotA.Item is { } a)
                yield return a;
            if (chem.BeakerSlotB.Item is { } b)
                yield return b;
        }
    }

    private bool TryGetSolution(EntityUid beaker, out Entity<SolutionComponent>? soln, out Solution solution)
    {
        soln = null;
        solution = default!;
        foreach (var (name, _) in _solution.EnumerateSolutions(beaker))
        {
            if (_solution.TryGetSolution(beaker, name, out soln, out var found))
            {
                solution = found;
                return true;
            }
        }

        return false;
    }
}
