using System.Numerics;
using Content.Server.AlertLevel;
using Content.Server.GameTicking.Rules;
using Content.Server.Imperial.Uplink.AssaultPod;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Shuttles.Components;
using Content.Server.Station.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Imperial.Epsilon;
using Content.Shared.Light.Components;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Content.Server.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Epsilon;

public enum EpsilonStage : byte
{
    Waiting,
    Announcing,
    LightsOut,
    Emergency,
    Launched,
    Done,
}
