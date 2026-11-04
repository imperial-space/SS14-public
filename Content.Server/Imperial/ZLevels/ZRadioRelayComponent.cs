using System.Numerics;
using Content.Server.Power.Components;
using Content.Server.Radio;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.ZLevels;

/// <summary>Невидимый ретранслятор рации на этаже (только сервер).</summary>
[RegisterComponent]
public sealed partial class ZRadioRelayComponent : Component;
