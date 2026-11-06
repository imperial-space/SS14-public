using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Electrocution;
using Content.Server.Flash;
using Content.Server.Lightning;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Medieval.Magic.TurnedToStone;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>Стержень с магом внутри.</summary>
[RegisterComponent]
public sealed partial class ImperialWizardRodComponent : Component
{
    [ViewVariables]
    public EntityUid? Wizard;

    [ViewVariables]
    public Vector2 Start;

    [ViewVariables]
    public float MaxDistance = 13f;

    /// <summary>70 + damage_bonus грубого урона пробитым.</summary>
    [ViewVariables]
    public float Damage = 70f;

    [ViewVariables]
    public HashSet<EntityUid> Hit = new();
}
