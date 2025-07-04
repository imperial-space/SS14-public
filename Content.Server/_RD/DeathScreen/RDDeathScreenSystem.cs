/*
 * Project: raincidation
 * File: RDDeathScreenSystem.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Content.Shared._RD;
using Content.Shared._RD.DeathScreen;
using Content.Shared.Mobs;

namespace Content.Server._RD.DeathScreen;

public sealed class RDDeathScreenSystem : RDEntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<RDDeathScreenComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<RDDeathScreenComponent> entity, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var player = GetSession(entity);
        if (player is null)
            return;

        RaiseNetworkEvent(new RDDeathScreenShowEvent("смерть", audioPath: "/Audio/Imperial/Medieval/DeathScreen/drown.ogg"), player.Channel);
    }
}
