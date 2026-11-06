using System.Numerics;
using Content.Server.Administration;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared.Administration;
using Content.Shared.Atmos.Components;
using Content.Shared.Gravity;
using Content.Shared.Materials;
using Content.Shared.Maps;
using Content.Shared.Station.Components;
using Robust.Shared.Audio;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

/// <summary>Гамма-автолат: предзагружен 50 листами стали и стекла (autolathe/loaded/upgraded/gamma).</summary>
[RegisterComponent]
public sealed partial class GammaAutolatheComponent : Component
{
    [DataField]
    public Dictionary<string, int> StartingMaterials = new() { ["Steel"] = 5000, ["Glass"] = 5000 };
}
