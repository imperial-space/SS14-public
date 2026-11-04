using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research;

/// <summary>
/// Технологии не из дерева TG и не перенесённые в него (закрытая сборка, новые ванильные):
/// сервер получает их дисциплины, консоль показывает их как самостоятельные узлы.
/// </summary>
public static class ImperialTgForeignTech
{
    private static readonly ProtoId<ImperialTgHostedPrototype> HostedId = "Default";

    public static bool IsForeign(IPrototypeManager proto, Content.Shared.Research.Prototypes.TechnologyPrototype tech)
    {
        if (tech.ID.StartsWith("ImperialTg") || tech.Hidden)
            return false;

        return !proto.TryIndex(HostedId, out var hosted) || !hosted.Technologies.ContainsKey(tech.ID);
    }
}
