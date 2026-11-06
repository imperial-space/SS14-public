using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Поплавок над местом ловли.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialFishingFloatComponent : Component
{
    /// <summary>Поклёвка: поплавок прыгает сильнее.</summary>
    [DataField, AutoNetworkedField]
    public bool Biting;

    /// <summary>Есть блесна: показывать огонёк.</summary>
    [DataField, AutoNetworkedField]
    public bool HasLure;

    /// <summary>Блесну пора крутить (зелёный огонёк).</summary>
    [DataField, AutoNetworkedField]
    public bool SpinReady;
}
