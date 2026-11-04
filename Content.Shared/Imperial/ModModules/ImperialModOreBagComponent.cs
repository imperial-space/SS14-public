using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>orebag: собирает руду под ногами, применение высыпает её.</summary>
[RegisterComponent]
public sealed partial class ImperialModOreBagComponent : Component
{
    public const string ContainerId = "imperial-mod-ores";

    [ViewVariables]
    public TimeSpan NextPickup;

    /// <summary>Не подбирать руду, пока носитель стоит на только что высыпанной куче.</summary>
    [ViewVariables]
    public System.Numerics.Vector2? DropPosition;
}
