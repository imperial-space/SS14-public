using Content.Shared.Storage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Content.Shared.Inventory;
namespace Content.Server.Imperial.HamMaggotson.PullableHelmet.Components;


[RegisterComponent]
public sealed partial class PullableHelmetComponent : Component
{
    [DataField("toggledPrototype", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ToggledPrototype = string.Empty;

    [DataField("untoggledPrototype", required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    [AutoNetworkedField]
    public string UntoggledPrototype = string.Empty;

    [DataField("toggled"), AutoNetworkedField]
    public bool Toggled = false;
    [DataField]
    public SlotFlags? Slot;
    [DataField("requiredSlot")]
    public SlotFlags RequiredFlags = SlotFlags.HEAD;
    [DataField]
    public LocId PullDownText = "pulldownhelmet-verb";

    [DataField]
    public LocId PullUpText = "pulluphelmet-verb";
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(2);
    [DataField]
    public SoundSpecifier PullUpSound { get; set; } = new SoundPathSpecifier("/Audio/Imperial/HamMaggotson/helmet_pullup.ogg");
    [DataField]
    public SoundSpecifier PullDownSound { get; set; } = new SoundPathSpecifier("/Audio/Imperial/HamMaggotson/helmet_pulldown.ogg");
    [DataField]
    public EntProtoId PullAction = "ToggleK63Helmet";

    [DataField]
    public EntityUid? Action;
}
