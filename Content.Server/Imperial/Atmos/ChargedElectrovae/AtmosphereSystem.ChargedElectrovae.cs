using Content.Server.Atmos.Components;
using Content.Server.Electrocution;
using Content.Server.Imperial.Atmos.ChargedElectrovae.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Database;
using Content.Shared.Imperial.Atmos;
using Content.Shared.Mobs.Components;
using Content.Shared.Power;
using Content.Shared.Power.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly BatterySystem _battery = default!;

    private EntityQuery<ApcPowerReceiverComponent> _powerReceiverQuery;
    private EntityQuery<MobStateComponent> _mobQuery;
    private EntityQuery<BatteryComponent> _batteryQuery;

    private void InitializeChargedElectrovae()
    {
        _powerReceiverQuery = GetEntityQuery<ApcPowerReceiverComponent>();
        _mobQuery = GetEntityQuery<MobStateComponent>();
        _batteryQuery = GetEntityQuery<BatteryComponent>();

        SubscribeLocalEvent<BatteryComponent, ComponentShutdown>(OnBatteryShutdown);
        SubscribeLocalEvent<ChargedElectrovaeAffectedComponent, ComponentShutdown>(OnChargedElectrovaeAffectedShutdown);
        SubscribeLocalEvent<ChargedElectrovaeAffectedComponent, ComponentRemove>(OnChargedElectrovaeAffectedRemove);
        SubscribeLocalEvent<ChargedElectrovaeAffectedComponent, RefreshChargeRateEvent>(OnRefreshChargeRate);
    }

    private void OnBatteryShutdown(Entity<BatteryComponent> ent, ref ComponentShutdown args)
    {
        RemCompDeferred<ChargedElectrovaeAffectedComponent>(ent);
    }

    private void OnRefreshChargeRate(Entity<ChargedElectrovaeAffectedComponent> ent, ref RefreshChargeRateEvent args)
    {
        var mixture = GetTileMixture((ent.Owner, null));
        if (mixture == null)
            return;

        var chargedMoles = mixture.GetMoles(Gas.ChargedElectrovae);
        if (chargedMoles < Atmospherics.ChargedElectrovaeMinimumMoles)
            return;

        var intensity = Math.Min(chargedMoles / Atmospherics.ChargedElectrovaeIntensityDivisor, 1f);
        if (intensity >= Atmospherics.ChargedElectrovaeMinimumEquipmentIntensity)
            args.NewChargeRate += intensity * Atmospherics.ChargedElectrovaeChargeRatePerIntensity;
    }

    private void OnChargedElectrovaeAffectedShutdown(
        Entity<ChargedElectrovaeAffectedComponent> ent,
        ref ComponentShutdown args)
    {
        if (_batteryQuery.TryComp(ent, out var battery))
            RestoreBatteryCapacity((ent, battery, ent.Comp));

        if (ent.Comp.OriginalNeedsPower != null && _powerReceiverQuery.TryComp(ent, out var receiver))
        {
            receiver.NeedsPower = ent.Comp.OriginalNeedsPower.Value;
            ent.Comp.OriginalNeedsPower = null;
        }
    }

    private void OnChargedElectrovaeAffectedRemove(
        Entity<ChargedElectrovaeAffectedComponent> ent,
        ref ComponentRemove args)
    {
        if (_batteryQuery.TryComp(ent, out var battery))
            _battery.RefreshChargeRate((ent.Owner, battery));
    }

    public void ChargedElectrovaeExpose(Entity<GridAtmosphereComponent?> ent, TileAtmosphere tile, float intensity)
    {
        if (!_atmosQuery.Resolve(ent, ref ent.Comp))
            return;

        if (!tile.ChargedEffect.Active)
            ent.Comp.ChargedElectrovaeTiles.Add(tile);

        tile.ChargedEffect.Active = true;
        tile.ChargedEffect.Intensity = Math.Clamp(intensity, 0f, 1f);
    }

    private void ProcessChargedElectrovae(
        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent> ent,
        TileAtmosphere tile)
    {
        var atmosphere = ent.Comp1;
        if (!tile.ChargedEffect.Active)
            return;

        var mixture = tile.Air;
        if (mixture == null)
        {
            tile.ChargedEffect = default;
            atmosphere.ChargedElectrovaeTiles.Remove(tile);
            return;
        }

        var chargedMoles = mixture.GetMoles(Gas.ChargedElectrovae);
        if (chargedMoles < Atmospherics.ChargedElectrovaeMinimumMoles)
        {
            RestorePowerRequirements(tile);
            tile.ChargedEffect = default;
            atmosphere.ChargedElectrovaeTiles.Remove(tile);
            return;
        }

        tile.ChargedEffect.Intensity = Math.Min(chargedMoles / Atmospherics.ChargedElectrovaeIntensityDivisor, 1f);
        tile.ChargedEffect.State = tile.ChargedEffect.Intensity switch
        {
            >= Atmospherics.ChargedElectrovaeHighIntensityThreshold => 3,
            >= Atmospherics.ChargedElectrovaeMediumIntensityThreshold => 2,
            >= Atmospherics.ChargedElectrovaeLowIntensityThreshold => 1,
            _ => 0,
        };

        _entSet.Clear();
        _lookup.GetLocalEntitiesIntersecting(tile.GridIndex, tile.GridIndices, _entSet, 0f);

        foreach (var entity in _entSet)
        {
            ProcessChargedElectrovaeBatteries(entity, tile.ChargedEffect.Intensity, chargedMoles);

            if (_powerReceiverQuery.TryComp(entity, out var receiver))
            {
                var affected = EnsureComp<ChargedElectrovaeAffectedComponent>(entity);
                ApplyChargedElectrovaePower((entity, affected), receiver, tile.ChargedEffect.Intensity);
            }

            if (!_mobQuery.HasComponent(entity))
                continue;

            var strikeChance = tile.ChargedEffect.Intensity * Atmospherics.ChargedElectrovaeLightningChanceMultiplier;
            if (_random.Prob(strikeChance))
                ApplyLightningStrike(entity, tile.ChargedEffect.Intensity);
        }
    }

    private void ProcessChargedElectrovaeBatteries(EntityUid entity, float intensity, float chargedMoles)
    {
        if (_batteryQuery.TryComp(entity, out var battery))
        {
            EnsureComp<ChargedElectrovaeAffectedComponent>(entity, out var affected);
            ProcessBattery((entity, battery, affected), intensity, chargedMoles);
        }

        if (!TryComp<ContainerManagerComponent>(entity, out var containerManager))
            return;

        foreach (var container in _containers.GetAllContainers(entity, containerManager))
        {
            foreach (var contained in container.ContainedEntities)
                ProcessChargedElectrovaeBatteries(contained, intensity, chargedMoles);
        }
    }

    private void RestorePowerRequirements(TileAtmosphere tile)
    {
        _entSet.Clear();
        _lookup.GetLocalEntitiesIntersecting(tile.GridIndex, tile.GridIndices, _entSet, 0f);

        foreach (var entity in _entSet)
            RemComp<ChargedElectrovaeAffectedComponent>(entity);
    }

    public void CleanupChargedElectrovaeEntities(Entity<GridAtmosphereComponent> grid)
    {
        var query = EntityQueryEnumerator<ChargedElectrovaeAffectedComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.GridUid != null && transform.GridUid != grid.Owner)
                continue;

            var mixture = GetTileMixture((uid, transform));
            if (mixture == null || mixture.GetMoles(Gas.ChargedElectrovae) < Atmospherics.ChargedElectrovaeMinimumMoles)
                RemComp<ChargedElectrovaeAffectedComponent>(uid);
        }
    }

    private void ProcessBattery(
        Entity<BatteryComponent, ChargedElectrovaeAffectedComponent> ent,
        float intensity,
        float chargedMoles)
    {
        var chargeRate = intensity >= Atmospherics.ChargedElectrovaeMinimumEquipmentIntensity
            ? intensity * Atmospherics.ChargedElectrovaeChargeRatePerIntensity
            : 0f;
        var chargeRateChanged = ent.Comp2.ChargedElectrovaeChargeRate != chargeRate;

        if (chargeRateChanged)
            ent.Comp2.ChargedElectrovaeChargeRate = chargeRate;

        var previousMaxCharge = ent.Comp1.MaxCharge;
        if (intensity >= Atmospherics.ChargedElectrovaeMinimumEquipmentIntensity)
            ExpandBatteryCapacity(ent, chargedMoles);
        else
            RestoreBatteryCapacity(ent);

        if (chargeRateChanged || previousMaxCharge != ent.Comp1.MaxCharge)
            _battery.RefreshChargeRate((ent.Owner, ent.Comp1));
    }

    private void ExpandBatteryCapacity(
        Entity<BatteryComponent, ChargedElectrovaeAffectedComponent> ent,
        float chargedMoles)
    {
        if (ent.Comp2.OriginalBatteryMaxCharge == null)
        {
            ent.Comp2.OriginalBatteryMaxCharge = ent.Comp1.MaxCharge;
            _adminLog.Add(LogType.AtmosPowerChanged, LogImpact.Low,
                $"Battery {ToPrettyString(ent)} capacity expanded by charged electrovae from {ent.Comp1.MaxCharge:F0}W to potentially 2x");
        }

        var expansionMultiplier =
            1f + (1f - MathF.Exp(-chargedMoles / Atmospherics.ChargedElectrovaeBatteryExpansionDecayConstant));
        _battery.SetMaxCharge((ent.Owner, ent.Comp1), ent.Comp2.OriginalBatteryMaxCharge.Value * expansionMultiplier);
    }

    private void RestoreBatteryCapacity(Entity<BatteryComponent, ChargedElectrovaeAffectedComponent> ent)
    {
        if (ent.Comp2.OriginalBatteryMaxCharge == null)
            return;

        _battery.SetMaxCharge((ent.Owner, ent.Comp1), ent.Comp2.OriginalBatteryMaxCharge.Value);
        ent.Comp2.OriginalBatteryMaxCharge = null;
    }

    private static void ApplyChargedElectrovaePower(
        Entity<ChargedElectrovaeAffectedComponent> ent,
        ApcPowerReceiverComponent receiver,
        float intensity)
    {
        if (intensity < Atmospherics.ChargedElectrovaeMinimumEquipmentIntensity)
        {
            if (ent.Comp.OriginalNeedsPower == null)
                return;

            receiver.NeedsPower = ent.Comp.OriginalNeedsPower.Value;
            ent.Comp.OriginalNeedsPower = null;
            return;
        }

        ent.Comp.OriginalNeedsPower ??= receiver.NeedsPower;
        receiver.NeedsPower = false;
    }

    private void ApplyLightningStrike(EntityUid target, float intensity)
    {
        var damage = (int) (Atmospherics.ChargedElectrovaeLightningBaseDamage +
                            intensity * Atmospherics.ChargedElectrovaeLightningDamagePerIntensity);
        var stunTime = TimeSpan.FromSeconds(Atmospherics.ChargedElectrovaeLightningBaseStunSeconds +
                                            intensity * Atmospherics.ChargedElectrovaeLightningStunSecondsPerIntensity);

        _adminLog.Add(LogType.Electrocution, LogImpact.Medium,
            $"{ToPrettyString(target):target} struck by charged electrovae lightning ({damage} damage, {stunTime.TotalSeconds:F1}s stun, intensity {intensity:F2})");

        _electrocution.TryDoElectrocution(
            target,
            null,
            damage,
            stunTime,
            refresh: true,
            siemensCoefficient: 1f,
            ignoreInsulation: false);
    }
}
