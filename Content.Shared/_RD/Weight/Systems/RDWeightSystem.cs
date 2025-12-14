/*
 * Project: raincidation
 * File: RDWeightSystem.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Content.Shared._RD.Weight.Components;
using Content.Shared._RD.Weight.Events;
using Content.Shared.Stacks;
using Robust.Shared.Configuration;

namespace Content.Shared._RD.Weight.Systems;

public sealed class RDWeightSystem : RDEntitySystem
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;

    private EntityQuery<RDWeightComponent> _weightQuery;
    private EntityQuery<StackComponent> _stackQuery;
    private int _maxUpdates;

    public override void Initialize()
    {
        base.Initialize();

        _weightQuery = GetEntityQuery<RDWeightComponent>();
        _stackQuery = GetEntityQuery<StackComponent>();
        _configuration.OnValueChanged(RDConfigVars.WeightMaxUpdates, value => _maxUpdates = value, true);

        SubscribeLocalEvent<RDWeightComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<RDWeightComponent, StackCountChangedEvent>(OnStackChanged);
        SubscribeLocalEvent<RDWeightComponent, EntParentChangedMessage>(OnParentChanged);
    }

    private void OnStartup(Entity<RDWeightComponent> entity, ref ComponentStartup args)
    {
        Refresh((entity, entity));
    }

    private void OnStackChanged(Entity<RDWeightComponent> entity, ref StackCountChangedEvent args)
    {
        Refresh((entity, entity));
    }

    private void OnParentChanged(Entity<RDWeightComponent> entity, ref EntParentChangedMessage args)
    {
        Refresh((entity, entity));

        if (args.OldParent is not null && !IsMap(args.OldParent.Value))
            Refresh(args.OldParent.Value);
    }

    public void Refresh(Entity<RDWeightComponent?> entity)
    {
        if (IsMap(entity))
        {
            Log.Error("You're a fucking psycho if you thought giving a card weight was a good, no, bummer. Your game will just wither and fall over.");
            return;
        }

        var calls = 0;
        while (true)
        {
            calls++;

            if (calls > _maxUpdates)
            {
                Log.Error("Max weight refresh iterations reached. Possible circular reference in entity hierarchy.");
                break;
            }

            if (!_weightQuery.Resolve(entity, ref entity.Comp, logMissing: false))
                break;

            var weight = 0f;

            var transform = Transform(entity);
            var parent = transform.ParentUid;
            var enumerator = transform.ChildEnumerator;

            while (enumerator.MoveNext(out var childUid))
            {
                weight += GetTotal(childUid);
            }

            entity.Comp.Inside = weight;
            DirtyField(entity, entity.Comp, nameof(RDWeightComponent.Inside));

            var ev = new RDWeightRefreshEvent((entity, entity.Comp), GetTotal(entity));
            RaiseLocalEvent(entity, ref ev);

            if (parent == EntityUid.Invalid)
                break;

            if (IsMap(parent))
                break;

            entity = parent;
        }
    }

    public float GetTotal(Entity<RDWeightComponent?> entity, bool refresh = false)
    {
        if (!_weightQuery.Resolve(entity, ref entity.Comp, logMissing: false))
            return RDWeightComponent.DefaultWeight;

        if (refresh)
            Refresh(entity);

        var count = _stackQuery.CompOrNull(entity)?.Count ?? 1;
        return entity.Comp.Inside + entity.Comp.Value * count;
    }
}
