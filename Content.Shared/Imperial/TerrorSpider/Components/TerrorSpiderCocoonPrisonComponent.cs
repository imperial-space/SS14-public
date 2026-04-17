using Content.Shared.Imperial.TerrorSpider.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.TerrorSpider.Components;

[RegisterComponent, NetworkedComponent, Access(typeof(SharedTerrorSpiderCocoonSystem))]
public sealed partial class TerrorSpiderCocoonPrisonComponent : Component
{
}
