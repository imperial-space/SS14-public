/*
 * Project: raincidation
 * File: RDWeightThrowModifierSystem.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Content.Shared._RD.Weight.Components;
using Content.Shared.Throwing;

namespace Content.Shared._RD.Weight.Systems;

public sealed class RDWeightThrowModifierSystem : EntitySystem
{
    [Dependency] private readonly RDWeightSystem _weight = default!;

    private EntityQuery<RDWeightThrowModifierComponent> _query;

    public override void Initialize()
    {
        base.Initialize();

        _query = GetEntityQuery<RDWeightThrowModifierComponent>();

        SubscribeLocalEvent<RDWeightThrowerModifierComponent, BeforeThrowEvent>(OnThrow);
    }

    private void OnThrow(Entity<RDWeightThrowerModifierComponent> entity, ref BeforeThrowEvent args)
    {
        if (!_query.TryGetComponent(args.ItemUid, out var component))
            return;

        args.Direction *= component.Curve.Calculate(_weight.GetTotal(args.ItemUid));
    }
}
