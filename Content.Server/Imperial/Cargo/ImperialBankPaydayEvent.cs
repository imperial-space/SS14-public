using Content.Shared.CartridgeLoader;
using System.Linq;
using Content.Server.Administration;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Stack;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Chat;
using Robust.Shared.Utility;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Cargo;
using Content.Shared.Interaction;
using Content.Shared.Roles;
using Content.Shared.Stacks;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cargo;

/// <summary>Зарплата выдана всем счетам (price_update TG: на этом шаге выходит экономический обзор).</summary>
[ByRefEvent]
public record struct ImperialBankPaydayEvent;
