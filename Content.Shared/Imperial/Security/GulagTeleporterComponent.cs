using Content.Shared.Actions;
using Content.Shared.DragDrop;
using Content.Shared.Mobs.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

// ─── Телепорт гулага (machinery/gulag_teleporter) ───────────────────────────

/// <summary>
/// Телепорт трудового лагеря. Общий — чтобы клиент разрешал перетаскивание заключённого.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GulagTeleporterComponent : Component
{
    public const string ContainerId = "gulag-teleporter-occupant";

    [ViewVariables]
    public ContainerSlot Occupant = default!;

    /// <summary>state_open: дверь открыта, внутрь можно посадить.</summary>
    [DataField, AutoNetworkedField]
    public bool Open = true;

    /// <summary>locked.</summary>
    [DataField, AutoNetworkedField]
    public bool Locked;

    /// <summary>breakout_time: 60 секунд, вдвое меньше для связанного при открытом замке.</summary>
    [DataField]
    public TimeSpan BreakoutTime = TimeSpan.FromSeconds(60);

    /// <summary>message_cooldown «дверь не поддаётся».</summary>
    [ViewVariables]
    public TimeSpan NextMessage;

    [ViewVariables]
    public bool BreakingOut;

    /// <summary>jumpsuit_type.</summary>
    [DataField]
    public string JumpsuitProto = "ClothingUniformJumpsuitPrisoner";

    /// <summary>shoes_type.</summary>
    [DataField]
    public string ShoesProto = "ClothingShoesColorOrange";
}
