using System.Linq;
using System.Numerics;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared.Bed.Sleep;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Lock;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>teleport/radius_turf/blink: случайный телепорт на 0–6 клеток.</summary>
[RegisterComponent]
public sealed partial class ImperialBlinkSpellComponent : Component
{
    [DataField]
    public float InnerRadius;

    [DataField]
    public float OuterRadius = 6f;

    [DataField]
    public SoundSpecifier? ArriveSound = new SoundPathSpecifier("/Audio/Imperial/Wizard/blink.ogg");
}
