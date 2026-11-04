using System.Numerics;
using Content.Shared.Imperial.ZLevels;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.ZLevels;

/// <summary>Отражение лампы верхнего этажа на нижнем (только у клиента).</summary>
[RegisterComponent]
public sealed partial class ZLightProxyComponent : Component;
