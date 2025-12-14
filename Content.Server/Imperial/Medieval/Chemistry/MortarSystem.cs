
using Content.Server.Chemistry.Containers.EntitySystems;
using Content.Server.DoAfter;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.DoAfter;
using Content.Shared.Imperial.Medieval.Chemistry;
using Content.Shared.Interaction;
using Content.Shared.Kitchen.Components;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Destructible;
using Content.Server.Stack;
using Robust.Server.Audio;
using Robust.Shared.Player;
using Robust.Shared.Audio;

namespace Content.Server.ChemistryRandomization;

public sealed class MortarSystem : EntitySystem
{
    [Dependency] private readonly DoAfterSystem _action = default!;
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly StackSystem _stackSystem = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<MortarComponent, InteractUsingEvent>(Interact);
        SubscribeLocalEvent<MortarComponent, MortarDoAfterEvent>(Finished);
    }
    public void Finished(EntityUid uid, MortarComponent component, MortarDoAfterEvent args)
    {
        if (!TryComp<StorageComponent>(uid, out var storage))
            return;
        if (!TryComp<SolutionContainerManagerComponent>(uid, out var solutioncomp))
            return;
        if (!_solution.TryGetSolution((uid, solutioncomp), "beaker", out var solutioncont, out var solution))
            return;
        foreach (var item in storage.Container.ContainedEntities)
        {
            if (!TryComp<ExtractableComponent>(item, out var extractable))
                continue;
            Solution transfer;
            if (extractable.JuiceSolution == null)
            {
                if (extractable.GrindableSolution == null)
                    continue;
                if (!_solution.TryGetSolution(item, extractable.GrindableSolution, out _, out var itemsolution))
                    continue;
                transfer = itemsolution;
            }
            else
            {
                transfer = extractable.JuiceSolution;
            }
            if (TryComp<StackComponent>(item, out var stack))
            {
                var totalVolume = transfer.Volume * stack.Count;
                if (totalVolume <= 0)
                    continue;

                var fitsCount = (int) (stack.Count * FixedPoint2.Min(solution.AvailableVolume / totalVolume + 0.01, 1));
                if (fitsCount <= 0)
                    continue;

                var scaledSolution = new Solution(transfer);
                scaledSolution.ScaleSolution(fitsCount);
                transfer = scaledSolution;

                _stackSystem.SetCount(item, stack.Count - fitsCount); // Setting to 0 will QueueDel
            }
            else
            {
                if (solution.Volume > solution.AvailableVolume)
                    continue;

                var dev = new DestructionEventArgs();
                RaiseLocalEvent(item, dev);

                QueueDel(item);
            }
            _solution.TryAddSolution(solutioncont.Value, transfer);
        }
        _audio.PlayEntity(new SoundPathSpecifier("/Audio/Machines/blender.ogg"), Filter.Broadcast(), uid, true);
    }
    public void Interact(EntityUid uid, MortarComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;
        if (!TryComp<StorageComponent>(uid, out var storage))
            return;
        if (storage.Container.Count == 0)
            return;
        if (!HasComp<MortarToolComponent>(args.Used))
            return;
        _action.TryStartDoAfter(new DoAfterArgs(_ent, args.User, TimeSpan.FromSeconds(10), new MortarDoAfterEvent(), uid, uid, args.Used));
        args.Handled = true;
    }
}
