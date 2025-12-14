using Content.Shared.Imperial.Medieval.ChemistryRandomization;
using Robust.Client.Player;

namespace Content.Client.Imperial.Medieval.ChemistryRandomization;

public sealed partial class ChemistryRandomizationSystem : EntitySystem
{
    [Dependency] private readonly SharedChemistryRandomizationSystem _chemRandom = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<SetChemistryRandomizationSeedMessage>(OnSetSeed);
        RequestSeed();
    }

    private void OnSetSeed(SetChemistryRandomizationSeedMessage args)
    {
        _chemRandom.Seed = args.Seed;
        _chemRandom.GeneratePotions();
    }

    private void RequestSeed()
    {
        if (_player.LocalSession == null)
            return;

        var message = new RequestChemistryRandomizationSeedMessage(_chemRandom.Seed, _player.LocalSession.Name);
        RaiseNetworkEvent(message);
    }
}
