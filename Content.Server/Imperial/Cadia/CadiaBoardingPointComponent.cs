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

/// <summary>Место прорыва абордажной группы на флагмане.</summary>
[RegisterComponent]
public sealed partial class CadiaBoardingPointComponent : Component
{
    [DataField(required: true)]
    public LocId Label;
}
