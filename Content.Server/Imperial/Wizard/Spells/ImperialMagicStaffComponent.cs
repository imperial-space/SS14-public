using System.Linq;
using System.Numerics;
using Content.Server.Polymorph.Systems;
using Content.Shared.Administration.Systems;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Doors.Components;
using Content.Shared.Examine;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Storage.Components;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>
/// /obj/item/gun/magic/staff: посох мага. Чужак (не маг) может пострадать сам;
/// «не в логове» — нельзя стрелять в логове волшебника.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialMagicStaffComponent : Component
{
    [DataField]
    public ImperialStaffIntruder Intruder;

    /// <summary>allow_intruder_use.</summary>
    [DataField]
    public bool AllowIntruder;

    /// <summary>no_den_usage.</summary>
    [DataField]
    public bool NoDenUsage;
}
