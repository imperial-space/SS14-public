using  Content.Shared.Inventory;

namespace Content.Shared.Actions;

public sealed partial class ActionGrantSystem
{
    // Imperial Weekly Mode Start
    public int SuppressMapInitGrants(Entity<ActionGrantComponent> ent)
    {
        var suppressed = ent.Comp.Actions.Count;
        if (suppressed == 0)
            return 0;

        ent.Comp.Actions.Clear();
        Dirty(ent.Owner, ent.Comp);
        return suppressed;
    }
    // Imperial Weekly Mode End
}
