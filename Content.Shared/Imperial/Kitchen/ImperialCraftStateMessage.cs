using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>Сервер → клиент: доступные для сборки рецепты.</summary>
[Serializable, NetSerializable]
public sealed class ImperialCraftStateMessage(HashSet<string> craftable) : EntityEventArgs
{
    public readonly HashSet<string> Craftable = craftable;
}
