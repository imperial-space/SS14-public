using System.Linq;
using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands;
using Content.Shared.Imperial.Security;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Security;

/// <summary>gun/medbeam: медицинский луч, лечащий цель, пока та в 8 клетках и на виду.</summary>
[RegisterComponent]
public sealed partial class MedbeamComponent : Component
{
    /// <summary>max_range.</summary>
    [DataField]
    public float MaxRange = 8f;

    /// <summary>check_delay: 1 секунда.</summary>
    [DataField]
    public TimeSpan CheckDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public DamageSpecifier Heal = new()
    {
        DamageDict = { ["Blunt"] = -1.5f, ["Slash"] = -1.5f, ["Piercing"] = -1f, ["Heat"] = -1.5f, ["Cold"] = -1f, ["Shock"] = -1.5f },
    };

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public EntityUid? User;

    [ViewVariables]
    public TimeSpan NextCheck;

    [DataField]
    public SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/Machines/scanning.ogg");

    [DataField]
    public SoundSpecifier LoopSound = new SoundPathSpecifier("/Audio/Ambience/Objects/emf_buzz.ogg");

    [DataField]
    public SoundSpecifier LostSound = new SoundPathSpecifier("/Audio/Machines/buzz-two.ogg");

    /// <summary>Гул, пока луч держится.</summary>
    [ViewVariables]
    public EntityUid? LoopStream;
}
