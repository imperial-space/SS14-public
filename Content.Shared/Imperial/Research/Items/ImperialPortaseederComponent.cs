using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Портативный экстрактор семян (plants/portaseeder): превращает урожай в сумке в семена.</summary>
[RegisterComponent]
public sealed partial class ImperialPortaseederComponent : Component;
