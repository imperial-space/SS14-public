using System.Linq;
using System.Numerics;
using Content.Server.Atmos.Monitor.Components;
using Content.Server.Atmos.Monitor.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Light.Components;
using Content.Server.Light.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Server.SurveillanceCamera;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Monitor.Components;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Electrocution;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Imperial.StationAi;
using Content.Shared.Jittering;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Content.Shared.RCD.Components;
using Content.Shared.Radio.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Stunnable;
using Content.Shared.SurveillanceCamera.Components;
using Content.Shared.VendingMachines;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.StationAi.Malf;

/// <summary>Роботизированная фабрика (transformer): кладёт людей и превращает в киборгов ИИ.</summary>
[RegisterComponent]
public sealed partial class ImperialMalfFactoryComponent : Component
{
    [ViewVariables]
    public EntityUid Ai;

    /// <summary>Перезарядка между превращениями (cooldown_duration 60 с у TG).</summary>
    [ViewVariables]
    public TimeSpan NextUse;
}
