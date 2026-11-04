using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Station.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Voidwalker;

/// <summary>/obj/effect/wisp_mobile: огонёк, на котором похищенный катается по пустоте.</summary>
[RegisterComponent]
public sealed partial class ImperialWispComponent : Component
{
    [ViewVariables]
    public EntityUid? Driver;

    [ViewVariables]
    public TimeSpan ReleaseAt;

    /// <summary>heal_per_food.</summary>
    [DataField]
    public float HealPerFood = 15f;
}
