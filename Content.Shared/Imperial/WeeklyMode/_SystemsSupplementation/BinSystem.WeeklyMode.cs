using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Content.Shared.Storage.Components;

namespace Content.Shared.Storage.EntitySystems;

public sealed partial class BinSystem
{
    // Imperial Weekly Mode Start
    public int SuppressInitialContents(Entity<BinComponent> ent)
    {
        var suppressed = ent.Comp.InitialContents.Count;
        if (suppressed == 0)
            return 0;

        ent.Comp.InitialContents.Clear();
        Dirty(ent.Owner, ent.Comp);
        return suppressed;
    }
    // Imperial Weekly Mode End
}
