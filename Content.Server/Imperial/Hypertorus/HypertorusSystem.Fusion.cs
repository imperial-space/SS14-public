using System.Linq;
using Content.Shared.Atmos.Components;
using Content.Server.Power.Components;
using Content.Shared.Atmos;
using Content.Shared.Imperial.Hypertorus;
using Content.Shared.Radiation.Components;

namespace Content.Server.Imperial.Hypertorus;

/// <summary>Основной процесс синтеза (hfr_main_processes.dm).</summary>
public sealed partial class HypertorusSystem
{
    #region Константы _hfr_defines.dm и atmos_core.dm

    private const double LightSpeed = 299792458;
    private const double PlanckLightConstant = 2e-16;
    private const double CalculatedH2Radius = 120e-4;
    private const double CalculatedTritRadius = 230e-3;
    private const double VoidConduction = 1e-2;
    private const float FusionMoleThreshold = 25;
    private const double InstabilityGasPowerFactor = 0.003;
    private const double ToroidVolumeBreakeven = 1000;
    private const float MetallicVoidConductivity = 0.38f;
    private const float HighEfficiencyConductivity = 0.975f;
    private const float MinPowerUsage = 50000;
    private const float IdlePowerUsage = 100;
    private const float DamageCapMultiplier = 0.005f;
    private const int IronChancePerFusionLevel = 17;
    private const float IronAccumulatedPerSecond = 0.005f;
    private const float IronOxygenHealPerSecond = IronAccumulatedPerSecond * (100 - IronChancePerFusionLevel) / 100f;
    private const float OxygenMolesConsumedPerIronHeal = 10 / IronOxygenHealPerSecond;
    private const double FusionInstabilityEndothermality = 4;
    private const double FusionMaximumTemperature = 1e8;
    private const float Tcmb = Atmospherics.TCMB;
    private const float MinimumMoleCount = 0.01f;

    private const int OverfullMinPowerLevel = 6;
    private const double OverfullMaxSafeColdFusionMoles = 2700;
    private const double OverfullMaxSafeHotFusionMoles = 1800;
    private const double OverfullMolarSlope = 1.0 / 200;
    private const double OverfullTemperatureSlope = OverfullMolarSlope * (OverfullMaxSafeColdFusionMoles - OverfullMaxSafeHotFusionMoles) / (FusionMaximumTemperature - 1);
    private const double OverfullConstant = -(OverfullMolarSlope * OverfullMaxSafeHotFusionMoles + OverfullTemperatureSlope * FusionMaximumTemperature);
    private const float SubcriticalMoles = 1200;
    private const float SubcriticalScale = 400;
    private const double ColdCoolantMaxRestore = 2.5;
    private const double ColdCoolantThreshold = 1e5;
    private static readonly double ColdCoolantScale = ColdCoolantMaxRestore / Math.Log10(ColdCoolantThreshold);
    private const float MaxSafeIron = 0.35f;
    private const float HypercriticalMoles = 10000;
    private const float HypercriticalScale = 0.002f;
    private const float HypercriticalMaxDamage = 20;

    private const float WeakSpillRate = 0.0005f;
    private const float WeakSpillChance = 1;
    private const float MediumSpillPressure = 10000;
    private const float MediumSpillInitial = 0.25f;
    private const float MediumSpillRate = 0.01f;
    private const float StrongSpillPressure = 12000;
    private const float StrongSpillInitial = 0.75f;
    private const float StrongSpillRate = 0.05f;

    /// <summary>SSair в SS13 обрабатывает атмос-машины раз в 0.5 с.</summary>
    private const float ProcessInterval = 0.5f;

    #endregion

    /// <summary>
    /// META_GAS_FUSION_POWER газов SS13, перенесённое на газы SS14 (с заменами недостающих газов).
    /// </summary>
    private static float FusionPower(Gas gas)
    {
        // Гелий есть только в закрытой сборке.
        if (gas == HeliumGas)
            return 7;

        return gas switch
        {
            Gas.WaterVapor => 8,
            Gas.HyperNoblium => 10,
            Gas.NitrousOxide => 10,
            Gas.Thermonium => 7, // нитрий
            Gas.Tritium => 5,
            Gas.BZ => 8,
            Gas.Ozonium => -10, // плюоксий
            Gas.Frezon => -5, // фреон
            Gas.Hydrogen => 2,
            Gas.AntiNoblium => 20,
            _ => 0,
        };
    }

    private static readonly Gas[] AllGases = Enum.GetValues<Gas>();

    /// <summary>Гелий по id: в публичной сборке его нет — тогда null.</summary>
    private static readonly Gas? HeliumGas = HypertorusFuelPrototype.ResolveGas("Helium");

    /// <summary>process_atmos: шаги по 0.5 с, как обработка атмос-машин в SS13.</summary>
    private void OnCoreAtmosUpdate(Entity<HypertorusCoreComponent> ent, ref AtmosDeviceUpdateEvent args)
    {
        ent.Comp.Accumulator += args.dt;
        if (ent.Comp.Accumulator < ProcessInterval)
            return;

        var secondsPerTick = ent.Comp.Accumulator;
        ent.Comp.Accumulator = 0;
        ProcessAtmos(ent, secondsPerTick);
    }

    private void ProcessAtmos(Entity<HypertorusCoreComponent> ent, float secondsPerTick)
    {
        var core = ent.Comp;
        if (!core.Active)
        {
            SetRadiation(ent, false);
            return;
        }

        if (!CheckPartConnectivity(ent))
        {
            Deactivate(ent);
            SetRadiation(ent, false);
            return;
        }

        var fusing = false;
        if (core.StartPower || core.PowerLevel > 0)
        {
            PlayAmbience(ent, secondsPerTick);
            fusing = FusionProcess(ent, secondsPerTick);
            ProcessModeratorOverflow(ent, secondsPerTick);
            ProcessDamageHeal(ent, secondsPerTick);
            CheckAlert(ent);
        }

        SetRadiation(ent, fusing);

        if (core.StartPower)
            RemoveWaste(ent, secondsPerTick);

        UpdateFusionStarted(ent);
        UpdateUi(ent);
    }

    /// <summary>radiation_pulse(src, max_range = 6, threshold = 0.3) каждый тик идущего синтеза.</summary>
    private void SetRadiation(EntityUid uid, bool active)
    {
        if (TryComp<RadiationSourceComponent>(uid, out var source) && source.Enabled != active)
            _radiation.SetSourceEnabled((uid, source), active);
    }

    #region fusion_process

    /// <summary>Возвращает, шла ли реакция (check_fuel) — для излучения.</summary>
    private bool FusionProcess(Entity<HypertorusCoreComponent> ent, float secondsPerTick)
    {
        var core = ent.Comp;
        var fuel = core.SelectedFuel is { } fuelId ? _proto.Index(fuelId) : null;

        if (CheckPowerUse(ent))
        {
            if (core.StartCooling)
            {
                InjectFromSideComponents(ent, fuel, secondsPerTick);
                ProcessInternalCooling(ent, secondsPerTick);
            }
        }
        else
        {
            // Без питания ручки принудительно встают в плохое положение.
            core.MagneticConstrictor = 100;
            core.HeatingConductor = 500;
            core.CurrentDamper = 0;
            core.FuelInjectionRate = 20;
            core.ModeratorInjectionRate = 50;
            core.WasteRemove = false;
            core.IronContent += 0.02f * core.PowerLevel * secondsPerTick;
        }

        UpdateTemperatureStatus(ent, secondsPerTick);

        var fusion = core.InternalFusion;
        var moderator = core.ModeratorInternal;

        var archivedHeat = core.FusionTemp;
        var volume = fusion.Volume * (core.MagneticConstrictor * 0.01);

        double energyConcentration = 1, positiveTemperature = 1, negativeTemperature = 1;
        var scaleFactor = volume * 0.5;

        var fuelList = new Dictionary<Gas, float>();
        var scaledFuel = new Dictionary<Gas, double>();
        if (fuel != null)
        {
            energyConcentration = fuel.EnergyConcentrationMultiplier;
            positiveTemperature = fuel.PositiveTemperatureMultiplier;
            negativeTemperature = fuel.NegativeTemperatureMultiplier;

            foreach (var gas in fuel.Requirements.Concat(fuel.PrimaryProducts).Distinct())
            {
                var amount = fusion.GetMoles(gas);
                fuelList[gas] = amount;
                scaledFuel[gas] = Math.Max((amount - FusionMoleThreshold) / scaleFactor, 0);
            }
        }

        var moderatorList = new Dictionary<Gas, float>();
        var scaledModerator = new Dictionary<Gas, double>();
        foreach (var gas in AllGases)
        {
            var amount = moderator.GetMoles(gas);
            moderatorList[gas] = amount;
            scaledModerator[gas] = Math.Max((amount - FusionMoleThreshold) / scaleFactor, 0);
        }

        double F(Gas? gas) => gas is { } g ? scaledFuel.GetValueOrDefault(g) : 0;
        double M(Gas gas) => scaledModerator.GetValueOrDefault(gas);
        // Нестабильность: размер фазового тора и «мощность» газов.
        var toroidalSize = 2 * Math.PI + Math.Atan((volume - ToroidVolumeBreakeven) / ToroidVolumeBreakeven);
        double gasPower = 0;
        foreach (var gas in AllGases)
        {
            gasPower += FusionPower(gas) * fusion.GetMoles(gas);
            gasPower += FusionPower(gas) * moderator.GetMoles(gas) * 0.75;
        }

        core.Instability = Modulus(Math.Pow(gasPower * InstabilityGasPowerFactor, 2), toroidalSize)
                           + core.CurrentDamper * 0.01 - core.IronContent * 0.05;
        var internalInstability = core.Instability * 0.5 < FusionInstabilityEndothermality ? 1 : -1;

        // Модификаторы газов модератора. Заукер — аммиак, нитрий — термониум, фреон — фрезон,
        // прото-нитрат — фазон; хилиум (оксид азота) в формулах энергии не учитывается, чтобы не задваивать N₂O.
        var energyModifiers = M(Gas.Nitrogen) * 0.35
                              + M(Gas.CarbonDioxide) * 0.55
                              + M(Gas.NitrousOxide) * 0.95
                              + M(Gas.Ammonia) * 1.55
                              + M(Gas.AntiNoblium) * 20;
        energyModifiers -= M(Gas.HyperNoblium) * 10
                           + M(Gas.WaterVapor) * 0.75
                           + M(Gas.Thermonium) * 0.15
                           + M(Gas.Frezon) * 1.15;

        var powerModifier = M(Gas.Oxygen) * 0.55
                            + M(Gas.CarbonDioxide) * 0.95
                            + M(Gas.Thermonium) * 1.45
                            + M(Gas.Ammonia) * 5.55
                            + M(Gas.Plasma) * 0.05
                            - M(Gas.NitrousOxide) * 0.05
                            - M(Gas.Frezon) * 0.75;

        var heatModifier = M(Gas.Plasma) * 1.25
                           - M(Gas.Nitrogen) * 0.75
                           - M(Gas.NitrousOxide) * 1.45
                           - M(Gas.Frezon) * 0.95;

        var radiationModifier = M(Gas.Frezon) * 1.15
                                - M(Gas.Nitrogen) * 0.45
                                - M(Gas.Plasma) * 0.95
                                + M(Gas.BZ) * 1.9
                                + M(Gas.Phazonium) * 0.1
                                + M(Gas.AntiNoblium) * 10;

        if (fuel != null)
        {
            var req1 = fuel.Requirements[0];
            var req2 = fuel.Requirements[1];
            var prod1 = fuel.PrimaryProduct(0);

            energyModifiers += F(req1) + F(req2);
            energyModifiers -= F(prod1);
            powerModifier += F(req2) * 1.05 - F(prod1) * 0.55;
            heatModifier += F(req1) * 1.15 + F(prod1) * 1.05;
            radiationModifier += F(prod1);
        }

        powerModifier = Math.Clamp(powerModifier, 0.25, 100);
        heatModifier = Math.Clamp(heatModifier, 0.25, 100);
        radiationModifier = Math.Clamp(radiationModifier, 0.005, 1000);

        // Основные расчёты.
        core.InternalPower = 0;
        core.Efficiency = VoidConduction;
        if (fuel != null)
        {
            var s1 = F(fuel.Requirements[0]);
            var s2 = F(fuel.Requirements[1]);
            core.InternalPower = (s1 * powerModifier / 100) * (s2 * powerModifier / 100)
                                 * (Math.PI * Math.Pow(2 * (s1 * CalculatedH2Radius) * (s2 * CalculatedTritRadius), 2))
                                 * core.Energy;
            core.Efficiency = VoidConduction * Math.Clamp(F(fuel.PrimaryProduct(0)), 1, 100);
        }

        core.Energy = energyModifiers * LightSpeed * LightSpeed * Math.Max(core.FusionTemp * heatModifier / 100, 1);
        core.Energy /= energyConcentration;
        core.Energy = Math.Clamp(core.Energy, 0, 1e35);
        if (double.IsNaN(core.Energy))
            core.Energy = 0;

        core.CoreTemperature = Math.Max(Tcmb, core.InternalPower * powerModifier / 1000);
        core.DeltaTemperature = archivedHeat - core.CoreTemperature;
        core.Conduction = -core.DeltaTemperature * (core.MagneticConstrictor * 0.001);
        core.Radiation = Math.Max(-(PlanckLightConstant / 5e-18) * radiationModifier * core.DeltaTemperature, 0);
        core.PowerOutput = core.Efficiency * (core.InternalPower - core.Conduction - core.Radiation);
        core.HeatLimiterModifier = 5 * Math.Pow(10, core.PowerLevel) * (core.HeatingConductor / 100);
        core.HeatOutputMin = -core.HeatLimiterModifier * 0.01 * negativeTemperature;
        core.HeatOutputMax = core.HeatLimiterModifier * positiveTemperature;
        core.HeatOutput = Math.Clamp(internalInstability * core.PowerOutput * heatModifier / 200,
            core.HeatOutputMin, core.HeatOutputMax);

        if (fuel == null || !CheckFuel(core, fuel))
            return false;

        var fuelConsumptionRate = Math.Clamp(core.FuelInjectionRate * 0.01 * 5 * core.PowerLevel, 0.05, 30);
        var consumptionAmount = fuelConsumptionRate * secondsPerTick;
        double productionAmount = core.PowerLevel is 3 or 4
            ? Math.Clamp(core.HeatOutput / 1000, 0, fuelConsumptionRate) * secondsPerTick
            : Math.Clamp(core.HeatOutput * 2 / Math.Pow(10, core.PowerLevel + 1), 0, fuelConsumptionRate) * secondsPerTick;

        // scaled_fuel_list[scaled_fuel_list[3]]: третий газ списка — первый побочный продукт.
        var dirtyProductionRate = F(fuel.PrimaryProduct(0)) / core.FuelInjectionRate;

        var internalOutput = new GasMixture(fusion.Volume);
        ModeratorFuelProcess(core, productionAmount, consumptionAmount, moderatorList, fuel, fuelList);

        var commonProduction = productionAmount * fuel.GasProductionMultiplier;
        ModeratorCommonProcess(ent, secondsPerTick, commonProduction, internalOutput, moderatorList, dirtyProductionRate, fuel);
        return true;

        static double Modulus(double x, double y) => x - Math.Floor(x / y) * y;
    }

    /// <summary>moderator_fuel_process: расход топлива и выход газов по ступеням уровня синтеза.</summary>
    private void ModeratorFuelProcess(HypertorusCoreComponent core, double productionAmount, double consumptionAmount,
        Dictionary<Gas, float> moderatorList, HypertorusFuelPrototype fuel, Dictionary<Gas, float> fuelList)
    {
        var fuelConsumption = (float) (consumptionAmount * 0.85 * fuel.FuelConsumptionMultiplier);
        var scaledProduction = (float) (productionAmount * fuel.GasProductionMultiplier);
        var fusion = core.InternalFusion;
        var moderator = core.ModeratorInternal;

        foreach (var gas in fuel.Requirements)
        {
            fusion.AdjustMoles(gas, -Math.Min(fuelList.GetValueOrDefault(gas), fuelConsumption));
        }

        foreach (var gas in fuel.PrimaryProducts)
        {
            fusion.AdjustMoles(gas, fuelConsumption * 0.5f);
        }

        void Tier(int index, float amount)
        {
            if (fuel.SecondaryProduct(index) is { } gas)
                moderator.AdjustMoles(gas, amount);
        }

        var plasma = moderatorList.GetValueOrDefault(Gas.Plasma);
        switch (core.PowerLevel)
        {
            case 1:
                Tier(0, scaledProduction * 0.95f);
                Tier(1, scaledProduction * 0.75f);
                break;
            case 2:
                Tier(0, scaledProduction * 1.65f);
                Tier(1, scaledProduction);
                if (plasma > 50)
                    Tier(2, scaledProduction * 1.15f);
                break;
            case 3:
                Tier(1, scaledProduction * 0.5f);
                Tier(2, scaledProduction * 0.45f);
                break;
            case 4:
                Tier(2, scaledProduction * 1.65f);
                Tier(3, scaledProduction * 1.25f);
                if (plasma > 50)
                    Tier(4, scaledProduction * 1.15f);
                break;
            case 5:
                Tier(3, scaledProduction * 0.65f);
                Tier(4, scaledProduction);
                Tier(5, scaledProduction * 0.75f);
                break;
            case 6:
                Tier(4, scaledProduction * 0.35f);
                Tier(5, scaledProduction);
                break;
        }
    }

    /// <summary>
    /// moderator_common_process: газы, общие для всех рецептов, влияние модератора на тепло и излучение,
    /// вывод в порт отходов и опасные эффекты.
    /// </summary>
    private void ModeratorCommonProcess(Entity<HypertorusCoreComponent> ent, float secondsPerTick, double scaledProductionD,
        GasMixture internalOutput, Dictionary<Gas, float> moderatorList, double dirtyProductionRate, HypertorusFuelPrototype fuel)
    {
        var core = ent.Comp;
        var moderator = core.ModeratorInternal;
        var fusion = core.InternalFusion;
        var scaledProduction = (float) scaledProductionD;
        float ML(Gas gas) => moderatorList.GetValueOrDefault(gas);
        void Consume(Gas gas, float amount) => moderator.AdjustMoles(gas, -Math.Min(moderator.GetMoles(gas), amount));

        switch (core.PowerLevel)
        {
            case 1:
                if (ML(Gas.Plasma) > 100)
                {
                    internalOutput.AdjustMoles(Gas.NitrousOxide, scaledProduction * 0.5f);
                    // В SS13 здесь плазма прибавляется, а не тратится (adjust_gas без минуса) — сохранено.
                    moderator.AdjustMoles(Gas.Plasma, Math.Min(moderator.GetMoles(Gas.Plasma), scaledProduction * 0.85f));
                }

                if (ML(Gas.BZ) > 150)
                {
                    internalOutput.AdjustMoles(Gas.CarbonDioxide, scaledProduction * 0.55f); // галон
                    moderator.AdjustMoles(Gas.BZ, Math.Min(moderator.GetMoles(Gas.BZ), scaledProduction * 0.95f));
                }
                break;
            case 2:
                if (ML(Gas.Plasma) > 50)
                {
                    internalOutput.AdjustMoles(Gas.BZ, scaledProduction * 1.8f);
                    moderator.AdjustMoles(Gas.Plasma, Math.Min(moderator.GetMoles(Gas.Plasma), scaledProduction * 1.75f));
                }

                if (ML(Gas.Phazonium) > 20)
                {
                    core.Radiation *= 1.55;
                    core.HeatOutput *= 1.025;
                    internalOutput.AdjustMoles(Gas.Thermonium, scaledProduction * 1.05f);
                    moderator.AdjustMoles(Gas.Phazonium, Math.Min(moderator.GetMoles(Gas.Phazonium), scaledProduction * 1.35f));
                }
                break;
            case 3 or 4:
                if (ML(Gas.Plasma) > 10)
                {
                    internalOutput.AdjustMoles(Gas.Frezon, scaledProduction * 0.15f);
                    internalOutput.AdjustMoles(Gas.Thermonium, scaledProduction * 1.05f);
                    moderator.AdjustMoles(Gas.Plasma, Math.Min(moderator.GetMoles(Gas.Plasma), scaledProduction * 0.45f));
                }

                if (ML(Gas.Frezon) > 50)
                {
                    core.HeatOutput *= 0.9;
                    core.Radiation *= 0.8;
                }

                if (ML(Gas.Phazonium) > 15)
                {
                    internalOutput.AdjustMoles(Gas.Thermonium, scaledProduction * 1.25f);
                    internalOutput.AdjustMoles(Gas.CarbonDioxide, scaledProduction * 1.15f); // галон
                    moderator.AdjustMoles(Gas.Phazonium, Math.Min(moderator.GetMoles(Gas.Phazonium), scaledProduction * 1.55f));
                    core.Radiation *= 1.95;
                    core.HeatOutput *= 1.25;
                }

                if (ML(Gas.BZ) > 100)
                {
                    internalOutput.AdjustMoles(Gas.NitrousOxide, scaledProduction * 1.5f); // хилиум
                    internalOutput.AdjustMoles(Gas.Phazonium, scaledProduction * 1.5f);
                    HallucinationPulse(ent, secondsPerTick);
                }
                break;
            case 5:
                if (ML(Gas.Plasma) > 15)
                {
                    internalOutput.AdjustMoles(Gas.Frezon, scaledProduction * 0.25f);
                    moderator.AdjustMoles(Gas.Plasma, Math.Min(moderator.GetMoles(Gas.Plasma), scaledProduction * 1.45f));
                }

                if (ML(Gas.Frezon) > 500)
                {
                    core.HeatOutput *= 0.5;
                    core.Radiation *= 0.2;
                }

                if (ML(Gas.Phazonium) > 50)
                {
                    internalOutput.AdjustMoles(Gas.Thermonium, scaledProduction * 1.95f);
                    internalOutput.AdjustMoles(Gas.Ozonium, scaledProduction); // плюоксий
                    moderator.AdjustMoles(Gas.Phazonium, Math.Min(moderator.GetMoles(Gas.Phazonium), scaledProduction * 1.35f));
                    core.Radiation *= 1.95;
                    core.HeatOutput *= 1.25;
                }

                if (ML(Gas.BZ) > 100)
                {
                    internalOutput.AdjustMoles(Gas.NitrousOxide, scaledProduction); // хилиум
                    internalOutput.AdjustMoles(Gas.Frezon, scaledProduction * 1.15f);
                    HallucinationPulse(ent, secondsPerTick);
                }

                HealiumRepair(core, moderatorList, scaledProduction, secondsPerTick);

                if (core.ModeratorTemp < 1e7 || ML(Gas.Plasma) > 100 && ML(Gas.BZ) > 50)
                    internalOutput.AdjustMoles(Gas.AntiNoblium, (float) (dirtyProductionRate * 0.9 / 0.065 * secondsPerTick));
                break;
            case 6:
                if (ML(Gas.Plasma) > 30)
                {
                    internalOutput.AdjustMoles(Gas.BZ, scaledProduction * 1.15f);
                    Consume(Gas.Plasma, scaledProduction * 1.45f);
                }

                if (ML(Gas.Phazonium) > 0)
                {
                    internalOutput.AdjustMoles(Gas.Ammonia, scaledProduction * 5.35f); // заукер
                    internalOutput.AdjustMoles(Gas.Thermonium, scaledProduction * 2.15f);
                    Consume(Gas.Phazonium, scaledProduction * 3.35f);
                    core.Radiation *= 2;
                    core.HeatOutput *= 2.25;
                }

                if (ML(Gas.BZ) > 0)
                {
                    HallucinationPulse(ent, secondsPerTick);
                    internalOutput.AdjustMoles(Gas.AntiNoblium, (float) (Math.Clamp(dirtyProductionRate / 0.045, 0, 10) * secondsPerTick));
                }

                HealiumRepair(core, moderatorList, scaledProduction, secondsPerTick);
                fusion.AdjustMoles(Gas.AntiNoblium, (float) (dirtyProductionRate * 0.01 / 0.095 * secondsPerTick));
                break;
        }

        // Температура смеси топлива меняется на величину тепловыделения.
        var temperatureModifier = fuel.TemperatureChangeMultiplier;
        var maxTemperature = FusionMaximumTemperature * temperatureModifier;
        if (core.FusionTemp <= maxTemperature)
        {
            core.FusionTemp = Math.Clamp(core.FusionTemp + core.HeatOutput * secondsPerTick, Tcmb, maxTemperature);
        }
        else
        {
            core.FusionTemp = Math.Max(Tcmb, core.FusionTemp - core.HeatLimiterModifier * 0.01 * secondsPerTick);
        }

        // Произведённые газы уходят в порт отходов, нагретые модератором или топливом.
        if (internalOutput.TotalMoles > 0 && GetPipeAir(core.WasteOutput) is { } outputPort)
        {
            internalOutput.Temperature = (float) Math.Min(Atmospherics.Tmax, moderator.TotalMoles > 0
                ? core.ModeratorTemp * HighEfficiencyConductivity
                : core.FusionTemp * MetallicVoidConductivity);
            _atmos.Merge(outputPort, internalOutput);
        }

        EvaporateModerator(core, secondsPerTick);
        CheckNuclearParticles(ent, moderatorList);
        CheckLightningArcs(ent, moderatorList);

        // Кислород быстро выжигает железо.
        if (ML(Gas.Oxygen) > 150 && core.IronContent > 0)
        {
            var ironRemoved = Math.Min(IronOxygenHealPerSecond * secondsPerTick, core.IronContent);
            core.IronContent -= ironRemoved;
            moderator.AdjustMoles(Gas.Oxygen, -Math.Min(moderator.GetMoles(Gas.Oxygen), ironRemoved * OxygenMolesConsumedPerIronHeal));
        }

        CheckGravityPulse(ent, secondsPerTick);
    }

    /// <summary>Хилиум (оксид азота) больше 100 моль чинит сильно повреждённое ядро на 5–6 уровне.</summary>
    private static void HealiumRepair(HypertorusCoreComponent core, Dictionary<Gas, float> moderatorList, float scaledProduction, float secondsPerTick)
    {
        var healium = moderatorList.GetValueOrDefault(Gas.NitrousOxide);
        if (healium <= 100 || core.CriticalThresholdProximity <= 400)
            return;

        core.CriticalThresholdProximity = Math.Max(core.CriticalThresholdProximity - healium / 100 * secondsPerTick, 0);
        core.ModeratorInternal.AdjustMoles(Gas.NitrousOxide,
            -Math.Min(core.ModeratorInternal.GetMoles(Gas.NitrousOxide), scaledProduction * 20));
    }

    /// <summary>evaporate_moderator: модератор понемногу выгорает, пока идёт синтез.</summary>
    private void EvaporateModerator(HypertorusCoreComponent core, float secondsPerTick)
    {
        if (core.PowerLevel == 0 || core.ModeratorInternal.TotalMoles <= 0)
            return;

        var ratio = 1 - Math.Pow(1 - 0.0005 * core.PowerLevel, secondsPerTick);
        core.ModeratorInternal.Remove((float) (core.ModeratorInternal.TotalMoles * ratio));
    }

    #endregion

    #region Охлаждение, впрыск, отходы

    /// <summary>process_internal_cooling: топливо отдаёт тепло модератору, модератор (или топливо) — контуру ядра.</summary>
    private void ProcessInternalCooling(Entity<HypertorusCoreComponent> ent, float secondsPerTick)
    {
        var core = ent.Comp;
        var fusion = core.InternalFusion;
        var moderator = core.ModeratorInternal;

        if (moderator.TotalMoles > 0 && fusion.TotalMoles > 0)
        {
            var fusionHc = _atmos.GetHeatCapacity(fusion, true);
            var moderatorHc = _atmos.GetHeatCapacity(moderator, true);
            var delta = core.FusionTemp - core.ModeratorTemp;
            var heat = (1 - Math.Pow(1 - MetallicVoidConductivity, secondsPerTick)) * delta * (fusionHc * moderatorHc / (fusionHc + moderatorHc));
            core.FusionTemp = Math.Max(core.FusionTemp - heat / fusionHc, Tcmb);
            core.ModeratorTemp = Math.Max(core.ModeratorTemp + heat / moderatorHc, Tcmb);
        }

        if (GetPipeAir(ent) is not { } coolant)
            return;

        // airs[1] ядра — часть трубы объёмом cooling_volume: из неё берётся 5 % газа.
        var share = Math.Clamp(core.CoolingVolume / Math.Max(coolant.Volume, 1), 0, 1);
        if (coolant.TotalMoles * share * 0.05f <= MinimumMoleCount)
            return;

        var removed = coolant.RemoveRatio(0.05f * share);
        var removedHc = _atmos.GetHeatCapacity(removed, true);
        if (moderator.TotalMoles > 0)
        {
            var moderatorHc = _atmos.GetHeatCapacity(moderator, true);
            var delta = removed.Temperature - core.ModeratorTemp;
            var heat = (1 - Math.Pow(1 - HighEfficiencyConductivity, secondsPerTick)) * delta * (removedHc * moderatorHc / (removedHc + moderatorHc));
            removed.Temperature = (float) Math.Max(removed.Temperature - heat / removedHc, Tcmb);
            core.ModeratorTemp = Math.Max(core.ModeratorTemp + heat / moderatorHc, Tcmb);
        }
        else if (fusion.TotalMoles > 0)
        {
            var fusionHc = _atmos.GetHeatCapacity(fusion, true);
            var delta = removed.Temperature - core.FusionTemp;
            var heat = (1 - Math.Pow(1 - MetallicVoidConductivity, secondsPerTick)) * delta * (removedHc * fusionHc / (removedHc + fusionHc));
            removed.Temperature = (float) Math.Max(removed.Temperature - heat / removedHc, Tcmb);
            core.FusionTemp = Math.Max(core.FusionTemp + heat / fusionHc, Tcmb);
        }

        _atmos.Merge(coolant, removed);
    }

    /// <summary>inject_from_side_components: газ из портов модератора и топлива внутрь ядра.</summary>
    private void InjectFromSideComponents(Entity<HypertorusCoreComponent> ent, HypertorusFuelPrototype? fuel, float secondsPerTick)
    {
        var core = ent.Comp;
        if (core.StartModerator && GetPipeAir(core.ModeratorInput) is { TotalMoles: > 0 } moderatorPort)
            MergeInto(core.ModeratorInternal, ref core.ModeratorTemp, moderatorPort.Remove(core.ModeratorInjectionRate * secondsPerTick));

        if (!core.StartFuel || fuel == null || GetPipeAir(core.FuelInput) is not { } fuelPort || !CheckGasRequirements(fuelPort, fuel))
            return;

        foreach (var gas in fuel.Requirements)
        {
            var removed = RemoveSpecific(fuelPort, gas, core.FuelInjectionRate * secondsPerTick / fuel.Requirements.Count);
            MergeInto(core.InternalFusion, ref core.FusionTemp, removed);
        }
    }

    /// <summary>remove_waste: отходы модератора по фильтру и побочные продукты топлива — в порт отходов.</summary>
    private void RemoveWaste(Entity<HypertorusCoreComponent> ent, float secondsPerTick)
    {
        var core = ent.Comp;
        if (!core.WasteRemove || GetPipeAir(core.WasteOutput) is not { } output)
            return;

        var filtering = core.ModeratorScrubbing.Count;
        foreach (var gas in core.ModeratorScrubbing)
        {
            if (core.ModeratorInternal.GetMoles(gas) <= 0)
                continue;

            _atmos.Merge(output, RemoveSpecific(core.ModeratorInternal, gas, core.ModeratorFilteringRate / filtering * secondsPerTick, core.ModeratorTemp));
        }

        if (core.SelectedFuel is not { } fuelId)
            return;

        foreach (var gas in _proto.Index(fuelId).PrimaryProducts)
        {
            var moles = core.InternalFusion.GetMoles(gas);
            if (moles <= 0)
                continue;

            _atmos.Merge(output, RemoveSpecific(core.InternalFusion, gas, (float) (moles * (1 - Math.Pow(1 - 0.25, secondsPerTick))), core.FusionTemp));
        }
    }

    /// <summary>remove_specific: забрать только один газ из смеси, с её температурой.</summary>
    private static GasMixture RemoveSpecific(GasMixture mixture, Gas gas, float amount, double? temperature = null)
    {
        var removed = new GasMixture { Temperature = (float) Math.Min(Atmospherics.Tmax, temperature ?? mixture.Temperature) };
        var moles = Math.Min(Math.Max(amount, 0), mixture.GetMoles(gas));
        if (moles <= 0)
            return removed;

        mixture.AdjustMoles(gas, -moles);
        removed.AdjustMoles(gas, moles);
        return removed;
    }

    /// <summary>Слить газ во внутреннюю смесь ядра, смешав температуры по теплоёмкости (без потолка Tmax).</summary>
    private void MergeInto(GasMixture target, ref double temperature, GasMixture giver)
    {
        var targetHc = _atmos.GetHeatCapacity(target, true);
        var giverHc = _atmos.GetHeatCapacity(giver, true);
        if (targetHc + giverHc > 0)
            temperature = (temperature * targetHc + giver.Temperature * giverHc) / (targetHc + giverHc);

        foreach (var gas in AllGases)
        {
            target.AdjustMoles(gas, giver.GetMoles(gas));
        }
    }

    /// <summary>Забрать долю внутренней смеси с её настоящей температурой (не выше Tmax).</summary>
    private static GasMixture RemoveRatioAt(GasMixture mixture, double temperature, float ratio)
    {
        var removed = mixture.RemoveRatio(ratio);
        removed.Temperature = (float) Math.Min(Atmospherics.Tmax, temperature);
        return removed;
    }

    private static bool CheckGasRequirements(GasMixture port, HypertorusFuelPrototype fuel)
    {
        foreach (var gas in fuel.Requirements)
        {
            if (port.GetMoles(gas) <= 0)
                return false;
        }

        return true;
    }

    private static bool CheckFuel(HypertorusCoreComponent core, HypertorusFuelPrototype fuel)
    {
        if (core.InternalFusion.TotalMoles <= 0)
            return false;

        foreach (var gas in fuel.Requirements)
        {
            if (core.InternalFusion.GetMoles(gas) < FusionMoleThreshold)
                return false;
        }

        return true;
    }

    /// <summary>check_power_use: 50 кВт за уровень синтеза (до 350 кВт на шестом).</summary>
    private bool CheckPowerUse(Entity<HypertorusCoreComponent> ent)
    {
        if (!TryComp<ApcPowerReceiverComponent>(ent, out var receiver))
            return true;

        receiver.Load = ent.Comp.StartPower ? (ent.Comp.PowerLevel + 1) * MinPowerUsage : IdlePowerUsage;
        return receiver.Powered;
    }

    /// <summary>update_temperature_status: температуры для интерфейса и уровень синтеза по температуре топлива.</summary>
    private void UpdateTemperatureStatus(Entity<HypertorusCoreComponent> ent, float secondsPerTick)
    {
        var core = ent.Comp;
        core.FusionTemperatureArchived = core.FusionTemperature;
        core.FusionTemperature = (float) core.FusionTemp;
        core.ModeratorTemperatureArchived = core.ModeratorTemperature;
        core.ModeratorTemperature = (float) core.ModeratorTemp;
        core.CoolantTemperatureArchived = core.CoolantTemperature;
        core.CoolantTemperature = GetPipeAir(ent)?.Temperature ?? 0;
        core.OutputTemperatureArchived = core.OutputTemperature;
        core.OutputTemperature = GetPipeAir(core.WasteOutput)?.Temperature ?? 0;
        core.TemperaturePeriod = secondsPerTick;

        core.PowerLevel = core.FusionTemperature switch
        {
            <= 500 => 0,
            <= 1e3f => 1,
            <= 1e4f => 2,
            <= 1e5f => 3,
            <= 1e6f => 4,
            <= 1e7f => 5,
            _ => 6,
        };
    }

    #endregion
}
