using System.Linq;
using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Chat.Systems;
using Content.Server.Imperial.Cargo;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Server.Lightning;
using Content.Shared.Actions;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Bitrunning;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>/datum/supply_pack/bitrunning: ящик доставки, наполняется содержимым корзины при появлении.</summary>
[RegisterComponent]
public sealed partial class BitrunningDeliveryCrateComponent : Component;
