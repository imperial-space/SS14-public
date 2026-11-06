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

/// <summary>/obj/item/gun/magic/wand: палочка без самозарядки, «выстрел в себя» — особый эффект.</summary>
[RegisterComponent]
public sealed partial class ImperialWandComponent : Component
{
    /// <summary>variable_charges: 25% — полный заряд, 50% — половина, 25% — треть.</summary>
    [DataField]
    public bool VariableCharges = true;

    /// <summary>zap_self(): эффект на себя.</summary>
    [DataField]
    public ImperialMagicBolt SelfEffect;

    [DataField]
    public SoundSpecifier? Sound;
}
