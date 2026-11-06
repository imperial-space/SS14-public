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

/// <summary>obj/item/petri_dish.</summary>
[RegisterComponent]
public sealed partial class VatPetriDishComponent : Component
{
    [ViewVariables]
    public VatSample? Sample;

    /// <summary>petri_dish/random: случайная проба при появлении.</summary>
    [DataField]
    public bool RandomSample;
}
