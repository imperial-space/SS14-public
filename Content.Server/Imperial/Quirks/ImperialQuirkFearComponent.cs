using Robust.Shared.Random;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Bed.Sleep;
using Content.Shared.Examine;
using Content.Shared.Fluids.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Storage.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Quirks;

/// <summary>
/// /datum/component/fearful: страх копится от источников и пропадает сам, пороги — испуг, ужас, паника, сердечный приступ.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialQuirkFearComponent : Component
{
    [DataField]
    public bool Claustrophobia;

    [DataField]
    public bool Nyctophobia;

    [DataField]
    public bool Monophobia;

    /// <summary>Фобия (/datum/brain_trauma/mild/phobia): случайная при появлении.</summary>
    [DataField]
    public bool Phobia;

    [ViewVariables]
    public string? PhobiaType;
    [ViewVariables]
    public float Buildup;
    [ViewVariables]
    public float LastBuildup;
    [ViewVariables]
    public TimeSpan NextTick;
    [ViewVariables]
    public TimeSpan NextPhobiaCheck;
    [ViewVariables]
    public TimeSpan ScareCooldown;
    [ViewVariables]
    public TimeSpan NextMessage;
    [ViewVariables]
    public TimeSpan PanicEnd;
}
