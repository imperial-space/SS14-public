using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>Клиент → сервер: какие рецепты сейчас можно собрать.</summary>
[Serializable, NetSerializable]
public sealed class ImperialCraftStateRequest : EntityEventArgs;
