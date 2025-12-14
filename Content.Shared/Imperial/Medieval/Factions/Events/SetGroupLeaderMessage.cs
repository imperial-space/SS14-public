using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Factions;

/// <summary>
/// Отправляется с клиента на сервер для назначения лидера группы.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class SetGroupLeaderMessage : EntityEventArgs
{
    public int Ent;
    public bool Leader;

    public SetGroupLeaderMessage(int ent, bool leader)
    {
        Ent = ent;
        Leader = leader;
    }
}
