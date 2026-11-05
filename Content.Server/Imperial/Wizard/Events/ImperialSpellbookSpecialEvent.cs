using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard;

/// <summary>Покупка записи с особым эффектом (призывы, перки, испытания): true — куплено.</summary>
[ByRefEvent]
public record struct ImperialSpellbookSpecialEvent(EntityUid User, EntityUid Book, ImperialSpellbookEntryPrototype Entry, bool Handled = false, bool Success = false);
