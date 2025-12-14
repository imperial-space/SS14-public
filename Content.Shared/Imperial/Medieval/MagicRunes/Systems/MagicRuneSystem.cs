using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

//=========================================================================
// MagicRuneSystem.cs
//=========================================================================
// Purpose: Main system for managing magic runes, scrolls, and knowledge
// Author: rhailrake
//=========================================================================

namespace Content.Shared.Imperial.Medieval.MagicRunes.Systems;

[Virtual]
public partial class MagicRuneSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedExplosionSystem _boomSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;


    public override void Initialize()
    {
        InitializeCore();
        InitializeUI();
    }
}
