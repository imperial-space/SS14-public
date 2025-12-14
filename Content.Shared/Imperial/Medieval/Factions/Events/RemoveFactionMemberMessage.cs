using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Factions;

/// <summary>
/// Отправляется с клиента на сервер при подтверждении удаления участника фракции.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class RemoveFactionMemberMessage : EntityEventArgs
{
    public int Ent;
    public int Performer;
    public string Details;
    public bool Headhunt;

    public RemoveFactionMemberMessage(int ent, int performer, string details, bool headhunt)
    {
        Ent = ent;
        Performer = performer;
        Details = details;
        Headhunt = headhunt;
    }
}
