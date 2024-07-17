using Content.Shared.Imperial.HardsuitInjection.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Imperial.HardsuitInjection.Components;


[Access(typeof(InjectSystem))]
[RegisterComponent]
public sealed partial class InjectComponent : Component
{
    [DataField("toggleInjectionAction", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ToggleInjectionAction = "ActionToggleInjection";

    [DataField("injectionAction", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string InjectionAction = "ActionInjection";


    [DataField("requiredSlot")]
    public SlotFlags RequiredFlags = SlotFlags.OUTERCLOTHING;

    [DataField("containerId")]
    public string ContainerId = "beakerSlot";


    [DataField("verbText")]
    public string VerbText = "hardsuitinjection-toggle";


    [DataField("delay")]
    public TimeSpan? Delay = TimeSpan.FromSeconds(30);

    [DataField("stripDelay")]
    public TimeSpan? StripDelay = TimeSpan.FromSeconds(10);


    [DataField("injectSound")]
    public SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");


    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? ToggleInjectionActionEntity;

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid? InjectionActionEntity;

    [ViewVariables(VVAccess.ReadWrite)]
    public ContainerSlot? Container;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool Locked = true;
}
