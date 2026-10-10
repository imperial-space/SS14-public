using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    public void SetGunsound(Entity<GunComponent> entity, SoundSpecifier sound)
    {
        entity.Comp.SoundGunshot = sound;
        entity.Comp.SoundGunshotModified = sound;

        Log.Error("sound changed holy fuck");
    }
}
