using System.Linq;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Chat.Systems;
using Content.Server.Electrocution;
using Content.Server.Fluids.EntitySystems;
using Content.Server.GameTicking;
using Content.Server.Pinpointer;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emag.Systems;
using Content.Shared.EntityTable;
using Content.Shared.Examine;
using Content.Shared.Fluids;
using Content.Shared.GameTicking;
using Content.Shared.Imperial.BluespaceTap;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.BluespaceTap;

/// <summary>Товар блюспейс-сборщика (bluespace_tap_product).</summary>
[DataDefinition]
public sealed partial class BluespaceTapProduct
{
    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public ProtoId<EntityTablePrototype> Common;

    [DataField(required: true)]
    public ProtoId<EntityTablePrototype> Uncommon;

    [DataField(required: true)]
    public ProtoId<EntityTablePrototype> Rare;

    /// <summary>Стоимость в очках; после каждой покупки растёт в 1.2 раза.</summary>
    [DataField(required: true)]
    public int Cost;

    /// <summary>Каждые сколько очков товар появляется сам (clothing_interval и т. д.).</summary>
    [DataField(required: true)]
    public float Interval;

    [DataField(required: true)]
    public LocId ProducedMessage;
}
