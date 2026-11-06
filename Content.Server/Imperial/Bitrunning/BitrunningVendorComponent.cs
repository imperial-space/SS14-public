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

/// <summary>/obj/machinery/computer/order_console/bitrunning (objects/vendor.dm, order_computer.dm).</summary>
[RegisterComponent]
public sealed partial class BitrunningVendorComponent : Component
{
    /// <summary>cargo_cost_multiplier: доставка грузовым рейсом дешевле на 35%.</summary>
    [DataField]
    public float CargoMultiplier = 0.65f;

    /// <summary>express_cost_multiplier.</summary>
    [DataField]
    public float ExpressMultiplier = 1f;

    /// <summary>cooldown_time.</summary>
    [DataField]
    public TimeSpan CooldownTime = TimeSpan.FromSeconds(10);

    /// <summary>CARGO_CRATE_VALUE: заказ рейсом не дешевле ящика.</summary>
    [DataField]
    public int CargoValue = 200;

    /// <summary>grocery_list: заказ → количество.</summary>
    [ViewVariables]
    public Dictionary<string, int> Cart = new();

    [ViewVariables]
    public TimeSpan NextOrder;

    [ViewVariables]
    public bool CooldownShown;

    [ViewVariables]
    public EntityUid? LastUser;
}
