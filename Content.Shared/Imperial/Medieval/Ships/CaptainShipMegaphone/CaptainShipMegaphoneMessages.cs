using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Ships;

[Serializable, NetSerializable]
public sealed class CaptainShipMegaphoneOpenMessage : EntityEventArgs
{
    public NetEntity Megaphone { get; }

    public CaptainShipMegaphoneOpenMessage(NetEntity megaphone)
    {
        Megaphone = megaphone;
    }
}

[Serializable, NetSerializable]
public sealed class CaptainShipMegaphoneSelectedCommandMessage : EntityEventArgs
{
    public NetEntity Megaphone { get; }

    public String Text { get; }

    public CaptainShipMegaphoneSelectedCommandMessage(NetEntity megaphone, String text)
    {
        Megaphone = megaphone;
        Text = text;
    }
}
