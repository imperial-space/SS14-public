using Content.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.DoAfter;
using Content.Shared.Emp;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Content.Shared.Light.Components;

namespace Content.Shared.Light.EntitySystems;

public abstract partial class SharedPoweredLightSystem
{
    // Imperial Weekly Mode Start
    /// <summary>
    ///     Clears the prototype that would be spawned into this light during map init.
    /// </summary>
    public bool ClearSpawnedPrototype(Entity<PoweredLightComponent> light)
    {
        if (light.Comp.HasLampOnSpawn == null)
            return false;

        light.Comp.HasLampOnSpawn = null;
        Dirty(light.Owner, light.Comp);
        return true;
    }
    // Imperial Weekly Mode End
}
