using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Imperial.Lavaland.BloodOfGodFountain;

namespace Content.Server.Imperial.Lavaland.BloodOfGodFountain;

public sealed class BloodOfGodFountainSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BloodOfGodFountainComponent, SolutionContainerChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<BloodOfGodFountainComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<BloodOfGodFountainComponent> ent, ref MapInitEvent args)
        => UpdateVisuals(ent);

    private void OnSolutionChanged(Entity<BloodOfGodFountainComponent> ent, ref SolutionContainerChangedEvent args)
    {
        if (args.SolutionId != "tank")
            return;
        UpdateVisuals(ent);
    }

    private void UpdateVisuals(EntityUid uid)
    {
        var hasLiquid = _solutions.TryGetSolution(uid, "tank", out _, out var solution)
                        && solution.Volume > 0;
        _appearance.SetData(uid, BloodFountainVisuals.State, hasLiquid);
    }
}
