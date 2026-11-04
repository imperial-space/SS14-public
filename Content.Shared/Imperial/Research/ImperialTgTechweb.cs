using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research;

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
