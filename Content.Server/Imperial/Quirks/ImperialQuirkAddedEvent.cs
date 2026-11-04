using System.Linq;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Quirks;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Traits;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Quirks;

/// <summary>Причуда выдана: системы эффектов делают одноразовую настройку (add_unique, post_add).</summary>
[ByRefEvent]
public readonly record struct ImperialQuirkAddedEvent(ProtoId<TraitPrototype> Quirk, ICommonSession? Player);
