using Content.Server.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticOpeningBladeSystem : EntitySystem
{
    [Dependency] private readonly SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly HereticSystem           _heretic     = default!;
    [Dependency] private readonly IRobustRandom           _random      = default!;

    private const float BleedChance = 0.35f;
    private const float BleedAmount = 10f;

    public override void Initialize()
    {
        base.Initialize();
    }
}
