using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>slimecross/industrial: поглощает плазму и производит предметы.</summary>
[RegisterComponent]
public sealed partial class SlimeIndustrialComponent : Component
{
    [DataField]
    public string Solution = "crossbreed";

    [DataField]
    public int PlasmaRequired = 2;

    [DataField]
    public EntProtoId? Item;

    /// <summary>Случайный пул: food, drink, foodordrink, coin, extract.</summary>
    [DataField]
    public string? RandomPool;

    [DataField]
    public int Amount = 1;

    /// <summary>Сколько единиц стопки выдать (iron/ten).</summary>
    [DataField]
    public int StackCount;

    /// <summary>darkblue: зелья огнеупорности одноразовые; yellow: батареи частично заряжены.</summary>
    [DataField]
    public string? AfterSpawn;

    [ViewVariables]
    public int Absorbed;

    [ViewVariables]
    public TimeSpan NextTick;

    [ViewVariables]
    public bool Working;
}
