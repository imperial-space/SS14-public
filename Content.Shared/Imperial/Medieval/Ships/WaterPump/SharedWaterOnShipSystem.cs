using System;
using Content.Shared.Imperial.Medieval.Ships.ShipDrowning;

namespace Content.Shared.Imperial.Medieval.Ships.WaterPump;

public sealed class SharedWaterOnShipSystem : EntitySystem
{
    public void RemoveWater(EntityUid ship, float count)
    {
        if (!TryComp<ShipDrowningComponent>(ship, out var shipDrowningcomp))
            return;

        var previousLevel = shipDrowningcomp.DrownLevel;
        shipDrowningcomp.DrownLevel = Math.Max(0, shipDrowningcomp.DrownLevel - count);
        if (shipDrowningcomp.DrownLevel != previousLevel)
            Dirty(ship, shipDrowningcomp);
    }
}
