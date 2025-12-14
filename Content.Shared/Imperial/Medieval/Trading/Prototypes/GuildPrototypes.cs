using Content.Shared.Store;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.Imperial.Medieval.Trading.Prototypes;



[Prototype]
[Serializable, NetSerializable]
public sealed partial class GuildTypePrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = null!;

    [DataField]
    public List<GuildTradingItem> Items = new();

    [DataField]
    public int MaximumGuilds = 1;

    [DataField] public ProtoId<GuildNamePrototype> Name;

    [DataField]
    public List<ProtoId<GuildIconPrototype>> Icons = new();

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<CurrencyPrototype>))]
    public string Currency = "Revent";
}

[Prototype]
[Serializable, NetSerializable]
public sealed partial class GuildIconPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = null!;

    [DataField]
    public string TexturePath = null!;
}


[Prototype, NetSerializable, Serializable]
public sealed partial class GuildNamePrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = null!;
    [DataField] public List<GuildNamePart> Parts = null!;
    [DataField] public int PartCount = 2;
    [DataField] public string Split = " ";
}

[DataDefinition, NetSerializable, Serializable]
public sealed partial class GuildNamePart
{
    [DataField] public string Text = "";
    [DataField] public int Min = 0;
    [DataField] public int Max = 5000;
}
