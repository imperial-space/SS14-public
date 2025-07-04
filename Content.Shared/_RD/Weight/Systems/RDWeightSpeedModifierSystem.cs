/*
 * Project: raincidation
 * File: RDWeightSpeedModifierSystem.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Content.Shared._RD.Mathematics.Extensions;
using Content.Shared._RD.Weight.Components;
using Content.Shared._RD.Weight.Events;
using Content.Shared.Movement.Systems;

namespace Content.Shared._RD.Weight.Systems;

public sealed class RDWeightSpeedModifierSystem : EntitySystem
{
    [Dependency] private readonly RDWeightSystem _weight = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifierSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RDWeightSpeedModifierComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<RDWeightSpeedModifierComponent, RDWeightRefreshEvent>(OnRefresh);
        SubscribeLocalEvent<RDWeightSpeedModifierComponent, RefreshMovementSpeedModifiersEvent>(OnSpeedRefresh);
    }

    private void OnStartup(Entity<RDWeightSpeedModifierComponent> entity, ref ComponentStartup args)
    {
        Refresh(entity);
    }

    private void OnRefresh(Entity<RDWeightSpeedModifierComponent> entity, ref RDWeightRefreshEvent args)
    {
        Refresh(entity, args.Total);
    }

    private void OnSpeedRefresh(Entity<RDWeightSpeedModifierComponent> entity, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(entity.Comp.Value);
    }

    private void Refresh(Entity<RDWeightSpeedModifierComponent> entity, float? total = null)
    {
        var value = entity.Comp.Curve.Calculate(total ?? _weight.GetTotal(entity.Owner));
        if (value.AboutEquals(entity.Comp.Value))
            return;

        entity.Comp.Value = value;
        DirtyField(entity, entity.Comp, nameof(RDWeightSpeedModifierComponent.Value));

        _movementSpeedModifierSystem.RefreshMovementSpeedModifiers(entity);
    }
}
