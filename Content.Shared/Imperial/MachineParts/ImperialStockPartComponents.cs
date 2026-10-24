using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.MachineParts;

/// <summary>Тип стандартной детали (/obj/item/stock_parts/*).</summary>
public enum ImperialStockPartType : byte
{
    MatterBin,
    Capacitor,
    Servo,
    MicroLaser,
    ScanningModule,
}

/// <summary>
/// Стандартная деталь машины с уровнем (rating). Засчитывается вместо универсальной детали
/// («Manipulator») в рамке машины, а её уровень влияет на работу машины.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialStockPartComponent : Component
{
    [DataField("partType", required: true), AutoNetworkedField]
    public ImperialStockPartType Type;

    [DataField, AutoNetworkedField]
    public int Tier = 1;
}

/// <summary>Быстрый заменитель деталей (/obj/item/storage/part_replacer).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialPartReplacerComponent : Component
{
    /// <summary>Блюспейс-версия: работает на расстоянии и с закрытой панелью.</summary>
    [DataField]
    public bool Bluespace;
}
