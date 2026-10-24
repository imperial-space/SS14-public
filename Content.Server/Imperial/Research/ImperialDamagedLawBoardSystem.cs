using Content.Server.Silicons.Laws;
using Content.Shared.Imperial.Research;
using Content.Shared.Silicons.Laws;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Research;

public sealed class ImperialDamagedLawBoardSystem : EntitySystem
{
    [Dependency] private readonly SiliconLawSystem _laws = default!;
    [Dependency] private readonly IonLawSystem _ion = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ImperialDamagedLawBoardComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ImperialDamagedLawBoardComponent, EntGotRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnMapInit(Entity<ImperialDamagedLawBoardComponent> ent, ref MapInitEvent args)
    {
        Regenerate(ent);
    }

    private void OnRemoved(Entity<ImperialDamagedLawBoardComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        Regenerate(ent);
    }

    private void Regenerate(Entity<ImperialDamagedLawBoardComponent> ent)
    {
        var laws = new List<SiliconLaw>();
        do
        {
            laws.Add(new SiliconLaw
            {
                LawString = _ion.GetIonLaw(),
                Order = laws.Count + 1,
            });
        }
        while (laws.Count < ent.Comp.MaxLaws && _random.Prob(ent.Comp.ExtraLawChance));

        _laws.SetLaws(laws, ent, null);
    }
}
