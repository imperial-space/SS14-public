using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Варп-разлом Хаоса: здесь колдуны и апостолы тратят силу варпа на призыв.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CadiaWarpRiftComponent : Component
{
    [DataField]
    public List<CadiaSummonEntry> Summons = new();

    /// <summary>Малый разлом закрывается сам, большой стоит до конца.</summary>
    [DataField]
    public TimeSpan? Lifetime;

    [ViewVariables]
    public TimeSpan? ClosesAt;
}
