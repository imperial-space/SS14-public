using System.Linq;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Fluids.Components;
using Content.Shared.Imperial.Xenobiology;
using Content.Shared.Imperial.Xenobiology.VatGrowing;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Xenobiology.VatGrowing;

/// <summary>obj/machinery/vatgrower.</summary>
[RegisterComponent]
public sealed partial class VatGrowerComponent : Component
{
    [DataField]
    public string Solution = "vat";

    /// <summary>Малый чан взрывается, если вырастить что-то крупнее мелкого существа.</summary>
    [DataField]
    public bool Small;

    [ViewVariables]
    public VatSample? Sample;

    [ViewVariables]
    public bool Resampler;

    [ViewVariables]
    public TimeSpan NextTick;

    [DataField]
    public float GrowChance = 0.1f;
}
