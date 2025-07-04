/*
 * Project: raincidation
 * File: RDWeightAlertSystem.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Content.Shared._RD.Weight.Components;
using Content.Shared._RD.Weight.Events;
using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Shared._RD.Weight.Systems;

public sealed class RDWeightAlertSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RDWeightAlertsComponent, RDWeightRefreshEvent>(OnRefresh);
    }

    private void OnRefresh(Entity<RDWeightAlertsComponent> entity, ref RDWeightRefreshEvent args)
    {
        var previous = entity.Comp.Alert;
        ProtoId<AlertPrototype>? current = null;

        foreach (var (id, range) in entity.Comp.Alerts)
        {
            if (args.Total <= range.Max && args.Total >= range.Min)
                current = id;
        }

        if (previous == current)
            return;

        if (previous is not null)
            _alerts.ClearAlert(entity, previous.Value);

        entity.Comp.Alert = current;
        DirtyField(entity, entity.Comp, nameof(RDWeightAlertsComponent.Alert));

        if (current is null)
            return;

        _alerts.ShowAlert(entity, current.Value);
    }
}
