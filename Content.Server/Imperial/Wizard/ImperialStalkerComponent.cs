using System.Linq;
using System.Numerics;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard;

/// <summary>/datum/brain_trauma/magic/stalker: призрак медленно ползёт к жертве; рядом — рвёт когтями.</summary>
[RegisterComponent]
public sealed partial class ImperialStalkerComponent : Component
{
    [ViewVariables]
    public EntityUid Target;

    /// <summary>on_life(): тик раз в 2 секунды.</summary>
    [ViewVariables]
    public TimeSpan NextTick;

    [DataField]
    public TimeSpan TickDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public float UpdateChance = 0.51f;
}
