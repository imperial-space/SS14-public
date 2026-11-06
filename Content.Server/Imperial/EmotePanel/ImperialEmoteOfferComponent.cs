using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Shared.Alert;
using Content.Shared.Bed.Sleep;
using Content.Shared.Chat;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Drunk;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.EmotePanel;
using Content.Shared.Imperial.Mood;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Placeable;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Toggleable;
using Content.Shared.Wagging;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.EmotePanel;

/// <summary>status_effect/offering TG: протянутый предмет-рука и те, кто может его принять.</summary>
[RegisterComponent]
public sealed partial class ImperialEmoteOfferComponent : Component
{
    [ViewVariables]
    public EntityUid Item;
    [ViewVariables]
    public ImperialHandItemKind Kind;

    /// <summary>offering/no_item_received/needs_resting: рука помогает подняться лежащему.</summary>
    [ViewVariables]
    public bool HelpUp;

    [ViewVariables]
    public ProtoId<AlertPrototype> Alert;
    [ViewVariables]
    public List<EntityUid> Takers = new();

    /// <summary>too_slowing_this_guy: кто уже попался на «слишком медленно».</summary>
    [ViewVariables]
    public HashSet<EntityUid> TooSlowing = new();

    [DataField]
    public TimeSpan TooSlowTime = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan TooSlowSeconds = TimeSpan.FromSeconds(0.5f);
}
