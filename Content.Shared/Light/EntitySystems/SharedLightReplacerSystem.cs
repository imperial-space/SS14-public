using Content.Shared.Light.Components;

namespace Content.Shared.Light.EntitySystems;

public abstract class SharedLightReplacerSystem : EntitySystem
{
    // Imperial Weekly Mode
    public int SuppressStartingContents(Entity<LightReplacerComponent> ent)
    {
        var suppressed = 0;
        foreach (var entry in ent.Comp.Contents)
        {
            suppressed += Math.Max(entry.Amount, 0);
        }

        if (ent.Comp.Contents.Count == 0)
            return 0;

        ent.Comp.Contents.Clear();
        Dirty(ent.Owner, ent.Comp);
        return suppressed;
    }
}
