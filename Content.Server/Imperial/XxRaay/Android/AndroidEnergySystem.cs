using System;
using Content.Shared.Alert;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Imperial.XxRaay.Android;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.XxRaay.Android;

/// <summary>
/// Серверная система, которая по уровню крови у андроида меняет его состояние
/// </summary>
public sealed class AndroidEnergySystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<AndroidEnergyComponent, BloodstreamComponent, MovementSpeedModifierComponent>();

        while (query.MoveNext(out var uid, out var energyComp, out var blood, out var moveSpeed))
        {
            if (curTime < energyComp.NextUpdate)
                continue;

            energyComp.NextUpdate = curTime + energyComp.UpdateInterval;

            var percent =
                _solutionContainer.ResolveSolution(uid, blood.BloodSolutionName, ref blood.BloodSolution, out var bloodSolution)
                    ? bloodSolution.FillFraction
                    : 0f;

            var maxSeverity = _alerts.GetMaxSeverity(energyComp.Alert);
            var minSeverity = _alerts.GetMinSeverity(energyComp.Alert);
            var severity = (short) Math.Clamp(Math.Round(percent * maxSeverity), minSeverity, maxSeverity);

            _alerts.ShowAlert(uid, energyComp.Alert, severity);

            energyComp.DefaultWalkSpeed ??= moveSpeed.BaseWalkSpeed;
            energyComp.DefaultSprintSpeed ??= moveSpeed.BaseSprintSpeed;

            var walk = energyComp.DefaultWalkSpeed ?? moveSpeed.BaseWalkSpeed;
            var sprint = energyComp.DefaultSprintSpeed ?? moveSpeed.BaseSprintSpeed;

            if (percent <= energyComp.CriticalEnergyThreshold)
            {
                walk *= energyComp.CriticalSpeedModifier;
                sprint *= energyComp.CriticalSpeedModifier;
            }
            else if (percent <= energyComp.LowEnergyThreshold)
            {
                walk *= energyComp.LowSpeedModifier;
                sprint *= energyComp.LowSpeedModifier;
            }

            _movementSpeed.ChangeBaseSpeed(uid, walk, sprint, 20, moveSpeed);
        }
    }
}

