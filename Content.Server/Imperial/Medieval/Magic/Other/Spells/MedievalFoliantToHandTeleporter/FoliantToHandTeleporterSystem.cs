using Content.Server.Imperial.Medieval.Magic.BindStoreOnEquip;
using Content.Shared.Imperial.Medieval.Magic;
using Content.Shared.Hands.EntitySystems;

namespace Content.Server.Imperial.Medieval.Magic.MedievalFoliantToHandTeleporter;
public sealed partial class FoliantToHandTeleporterSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FoliantToHandTeleporterComponent, MedievalAfterSpawnEntityBySpellEvent>(FindFoliant);
    }

    private void FindFoliant(EntityUid uid, FoliantToHandTeleporterComponent component, MedievalAfterSpawnEntityBySpellEvent args)
    {
        EntityUid playerUid = args.Performer;
        var query = EntityQueryEnumerator<BindStoreOnEquipComponent>();

        while (query.MoveNext(out var folliantUID, out var bindComp))
        {
            if (bindComp.BindedEntity == playerUid)
            {
                _handsSystem.TryForcePickupAnyHand(playerUid, folliantUID);
                break;
            }
        }
    }
}
