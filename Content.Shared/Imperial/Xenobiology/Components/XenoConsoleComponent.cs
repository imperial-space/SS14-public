using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>
/// Компонент ксенобиологической консоли.
/// Управляет хранилищами обезьян/слаймов и активным призраком-оператором.
/// </summary>
[RegisterComponent]
public sealed partial class XenoConsoleComponent : Component
{
    /// <summary>Текущая активная камера-призрак, если консоль используется.</summary>
    public EntityUid? GhostEntity;

    /// <summary>Прототип обезьяны, которую можно хранить в консоли.</summary>
    [DataField]
    public EntProtoId MonkeyProto = "XenoMonkey";

    /// <summary>Список запомненных обезьян (физически остаются на месте).</summary>
    public List<EntityUid> MonkeyStorage = new();

    /// <summary>Список запомненных слаймов (физически остаются на месте).</summary>
    public List<EntityUid> SlimeStorage = new();

    /// <summary>Название раствора с реагентами для инъекций слаймам.</summary>
    [DataField]
    public string PotionSolutionName = "xeno_console_potion";
}

