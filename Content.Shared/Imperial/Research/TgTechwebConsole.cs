using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research;

/// <summary>
/// Состояние консоли techweb TG для окна (tgui Techweb): блокировка, очередь изучения сервера
/// и прирост очков в секунду.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialTgTechwebConsoleComponent : Component
{
    /// <summary>toggleLock: заблокированная консоль показывает только окно разблокировки.</summary>
    [DataField, AutoNetworkedField]
    public bool Locked;

    /// <summary>queue_nodes: узлы в очереди сервера.</summary>
    [AutoNetworkedField]
    public List<string> Queue = new();

    /// <summary>Кто поставил узел в очередь (подсказка «Added by»).</summary>
    [AutoNetworkedField]
    public List<string> QueueBy = new();

    /// <summary>points_last_tick: прирост очков за секунду.</summary>
    [AutoNetworkedField]
    public int PointsPerSecond;

    /// <summary>sec_protocols: протоколы безопасности (выключаются взломом).</summary>
    [AutoNetworkedField]
    public bool SecProtocols = true;
}

[Serializable, NetSerializable]
public sealed class ImperialTgEnqueueNodeMessage : BoundUserInterfaceMessage
{
    public readonly string Node;

    public ImperialTgEnqueueNodeMessage(string node)
    {
        Node = node;
    }
}

[Serializable, NetSerializable]
public sealed class ImperialTgDequeueNodeMessage : BoundUserInterfaceMessage
{
    public readonly string Node;

    public ImperialTgDequeueNodeMessage(string node)
    {
        Node = node;
    }
}

[Serializable, NetSerializable]
public sealed class ImperialTgToggleLockMessage : BoundUserInterfaceMessage;

/// <summary>researchNode: изучить узел с учётом скидочных экспериментов.</summary>
[Serializable, NetSerializable]
public sealed class ImperialTgResearchNodeMessage : BoundUserInterfaceMessage
{
    public readonly string Node;

    public ImperialTgResearchNodeMessage(string node)
    {
        Node = node;
    }
}

/// <summary>
/// Данные узла techweb TG, которых нет у TechnologyPrototype: каналы объявления (announce_channels).
/// </summary>
[Robust.Shared.Prototypes.Prototype("imperialTgNode")]
public sealed partial class ImperialTgNodePrototype : Robust.Shared.Prototypes.IPrototype
{
    /// <summary>ID — как у технологии (ImperialTg...).</summary>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public List<Robust.Shared.Prototypes.ProtoId<Content.Shared.Radio.RadioChannelPrototype>> Channels = new();
}

/// <summary>Общая логика цены узлов techweb TG для сервера и окна консоли.</summary>
public static class ImperialTgTechweb
{
    /// <summary>Множитель очков: цены и скидки TG × 75.</summary>
    public const int PointScale = 75;

    /// <summary>Пассивный доход сети в секунду (TECHWEB_SINGLE_SERVER_INCOME = 1).</summary>
    public const int ServerIncome = 1 * PointScale;

    /// <summary>
    /// Скидка узла от выполненных discount_experiments (не больше цены).
    /// </summary>
    public static int GetDiscount(Robust.Shared.Prototypes.IPrototypeManager proto, Content.Shared.Research.Prototypes.TechnologyPrototype tech, ICollection<string> completed)
    {
        var discount = 0;
        foreach (var exp in proto.EnumeratePrototypes<Experiments.TgExperimentPrototype>())
        {
            if (exp.Required || !completed.Contains(exp.ID) || NodeTechId(exp.Node) != tech.ID)
                continue;

            discount += exp.Discount * PointScale;
        }

        return Math.Min(discount, tech.Cost);
    }

    /// <summary>ID технологии по ID узла TG (snake_case → ImperialTgCamelCase).</summary>
    public static string NodeTechId(string node)
    {
        var parts = node.Split('_');
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
                parts[i] = parts[i].Substring(0, 1).ToUpperInvariant() + parts[i].Substring(1);
        }

        return "ImperialTg" + string.Concat(parts);
    }
}

/// <summary>Технологии SS14, чьи рецепты перенесены в узлы TG (генерируется вместе с деревом).</summary>
[Prototype("imperialTgHosted")]
public sealed partial class ImperialTgHostedPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Технология SS14 → узел TG, с изучением которого она считается изученной.</summary>
    [DataField]
    public Dictionary<string, string> Technologies = new();
}

/// <summary>
/// Технологии не из дерева TG и не перенесённые в него (закрытая сборка, новые ванильные):
/// сервер получает их дисциплины, консоль показывает их как самостоятельные узлы.
/// </summary>
public static class ImperialTgForeignTech
{
    public static bool IsForeign(IPrototypeManager proto, Content.Shared.Research.Prototypes.TechnologyPrototype tech)
    {
        if (tech.ID.StartsWith("ImperialTg") || tech.Hidden)
            return false;

        return !proto.TryIndex<ImperialTgHostedPrototype>("Default", out var hosted) || !hosted.Technologies.ContainsKey(tech.ID);
    }
}
