using System.Numerics;
using Content.Server.Popups;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>Абордажная рампа «Валькирии»: отсюда штурмовая группа прорывается на флагман Хаоса.</summary>
[RegisterComponent]
public sealed partial class CadiaBoardingRampComponent : Component
{
    [DataField]
    public float Radius = 1.5f;

    [DataField]
    public int Capacity = 8;

    /// <summary>Как близко к флагману должна подойти «Валькирия».</summary>
    [DataField]
    public float Reach = 45f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    [ViewVariables]
    public TimeSpan NextUse;
}
