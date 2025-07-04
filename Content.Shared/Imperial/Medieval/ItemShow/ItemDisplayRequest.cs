using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.ItemShow;

[Serializable, NetSerializable]
public sealed class ItemDisplayRequest : EntityEventArgs
{
    public NetEntity ItemUid { get; set; }

    public ItemDisplayRequest(NetEntity itemUid)
    {
        ItemUid = itemUid;
    }
}
