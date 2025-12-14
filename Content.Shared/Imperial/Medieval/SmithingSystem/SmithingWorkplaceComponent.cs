using Content.Shared.Containers.ItemSlots;
using Content.Shared.Imperial.Medieval.SmithingSystem.Bui;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.SmithingSystem;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SmithingWorkplaceComponent : Component
{
    [DataField]
    public SoundSpecifier HitSound = new SoundPathSpecifier("/Audio/Imperial/Medieval/Crafting/Smithing/anvil_hit.ogg");

    [DataField]
    public ItemSlot WorkpieceSlot = new();

    [DataField]
    public string SmithingToolTag = "SmithingTool";

    [DataField, AutoNetworkedField]
    public EntProtoId EffectProto = "EffectSparks";

    public Entity<SmithingWorkpieceComponent>? Workpiece;

    public SmithGameState? GameState { get; set; }
}
