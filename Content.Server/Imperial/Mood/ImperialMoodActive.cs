using System.Linq;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Shared.Alert;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Mood;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Drunk;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Mood;

/// <summary>Активный мудлет.</summary>
public sealed class ImperialMoodActive
{
    public ProtoId<ImperialMoodEventPrototype> Event;
    public int Change;
    public string Description = string.Empty;
    public bool Hidden;
    public float Timeout;
    public TimeSpan? Expires;
    public int Priority;
}
