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

/// <summary>pointed/mind_transfer: обмен телами с целью рядом, оба теряют сознание на 40 секунд.</summary>
[RegisterComponent]
public sealed partial class ImperialMindSwapSpellComponent : Component
{
    [DataField]
    public TimeSpan Unconscious = TimeSpan.FromSeconds(40);
}
