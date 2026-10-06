using Robust.Shared.Random;
using Content.Shared.Atmos;
using Content.Shared.Examine;
using Content.Shared.Imperial.Fission;
using Content.Shared.Light.Components;
using Robust.Shared.Audio;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Fission;

public sealed partial class FissionSystem
{
    /// <summary>Эффекты газов теплоносителя (reactor_gas_effect/coolant): перегрев, шанс аварий, реактивность.</summary>
    private static readonly Dictionary<Gas, (float Overheat, float Event, float Reactivity)> CoolantEffects = new()
    {
        [Gas.Nitrogen] = (600, -20, 0),
        [Gas.NitrousOxide] = (300, -20, 0),
        [Gas.Oxygen] = (0, 50, 0.8f),
        [Gas.Plasma] = (200, 0, 0.2f),
        [Gas.CarbonDioxide] = (0, -60, 0),
    };

    /// <summary>Эффекты газов-замедлителей (reactor_gas_effect/moderator). Плюоксий заменён озонием.</summary>
    private static readonly Dictionary<Gas, ModeratorEffect> ModeratorEffects = new()
    {
        [Gas.Nitrogen] = new ModeratorEffect { Control = 4, Radiation = 0.04f },
        [Gas.Ozonium] = new ModeratorEffect { Control = 6 },
        [Gas.CarbonDioxide] = new ModeratorEffect { Control = 8, Radiation = 0.08f },
        [Gas.BZ] = new ModeratorEffect { Permeability = 0.2f },
        [Gas.WaterVapor] = new ModeratorEffect { Permeability = 0.4f },
        [Gas.HyperNoblium] = new ModeratorEffect { Permeability = 2 },
        [Gas.NitrousOxide] = new ModeratorEffect { Depletion = 0.67f },
        [Gas.Plasma] = new ModeratorEffect { PowerPerMole = 10, HeatPerMole = 5 },
        [Gas.Tritium] = new ModeratorEffect { PowerPerMole = 100, HeatPerMole = 50, Radiation = 0.2f },
        [Gas.Oxygen] = new ModeratorEffect { PowerMod = 10 },
    };

    private struct ModeratorEffect
    {
        public float Control;
        public float Radiation;
        public float Permeability;
        public float Depletion;
        public float PowerPerMole;
        public float HeatPerMole;
        public float PowerMod;
    }

    public static string? CoolantDescription(Gas gas) => CoolantEffects.ContainsKey(gas) ? $"fission-gas-coolant-{gas}" : null;
    public static string? ModeratorDescription(Gas gas) => ModeratorEffects.ContainsKey(gas) ? $"fission-gas-moderator-{gas}" : null;

    private void InitializeReactor()
    {
        SubscribeLocalEvent<FissionReactorComponent, MapInitEvent>(OnReactorMapInit);
        SubscribeLocalEvent<FissionReactorComponent, ExaminedEvent>(OnReactorExamined);
        SubscribeLocalEvent<FissionReactorComponent, Content.Shared.Explosion.GetExplosionResistanceEvent>(OnReactorExplosionResistance);
        SubscribeLocalEvent<FissionReactorComponent, Content.Shared.Damage.Systems.BeforeDamageChangedEvent>(OnReactorBeforeDamage);
        InitializeRepair();
    }

    private void OnReactorMapInit(Entity<FissionReactorComponent> ent, ref MapInitEvent args)
    {
        var air = ent.Comp.Air;
        air.Volume = 1000;
        air.AdjustMoles(Gas.Oxygen, Atmospherics.OxygenMolesStandard * 0.6f);
        air.AdjustMoles(Gas.Nitrogen, Atmospherics.NitrogenMolesStandard * 0.6f);
        air.Temperature = Atmospherics.T20C;
        ent.Comp.ModeratorGas.Volume = 500;

        ent.Comp.GasAbsorptionEffectiveness = _random.Next(5, 7) / 10f;
        ent.Comp.GasAbsorptionConstant = ent.Comp.GasAbsorptionEffectiveness;
        ent.Comp.NextProcess = _timing.CurTime + FissionReactorComponent.ProcessInterval;

        _ambient.SetAmbience(ent, false);
        _light.SetEnabled(ent, false);
        UpdateReactorVisuals(ent);
    }

    private EntityUid? _explosionHit;

    private void OnReactorExplosionResistance(Entity<FissionReactorComponent> ent, ref Content.Shared.Explosion.GetExplosionResistanceEvent args)
    {
        _explosionHit = ent.Owner;
    }

    /// <summary>
    /// Ядро неразрушимо; ex_act: опустошающий взрыв ломает его (с выбросом), тяжёлый — 100–300 урона, лёгкий — 30–150.
    /// Интенсивность клетки восстанавливается из урона взрыва (15 ед. на единицу интенсивности у Default).
    /// </summary>
    private void OnReactorBeforeDamage(Entity<FissionReactorComponent> ent, ref Content.Shared.Damage.Systems.BeforeDamageChangedEvent args)
    {
        args.Cancelled = true;
        if (_explosionHit != ent.Owner)
            return;

        _explosionHit = null;
        var intensity = args.Damage.GetTotal().Float() / 15;
        if (intensity >= 40)
            SetBroken(ent);
        else if (intensity >= 15)
            AdjustDamage(ent, _random.Next(100, 301));
        else if (intensity > 0)
            AdjustDamage(ent, _random.Next(30, 151));
    }

    private void OnReactorExamined(Entity<FissionReactorComponent> ent, ref ExaminedEvent args)
    {
        var reactor = ent.Comp;
        if (reactor.Broken)
        {
            args.PushMarkup(Loc.GetString("fission-reactor-examine-broken"));
            args.PushMarkup(Loc.GetString($"fission-reactor-examine-repair-{reactor.RepairStep}"));
            return;
        }

        args.PushMarkup(Loc.GetString("fission-reactor-examine-integrity", ("integrity", GetIntegrity(reactor).ToString("0.##"))));
        args.PushMarkup(Loc.GetString("fission-reactor-examine-rods", ("rods", reactor.ControlRodsRemaining)));
        if (reactor.Venting)
            args.PushMarkup(Loc.GetString("fission-reactor-examine-venting"));
        if (reactor.ControlRodsRemaining < FissionReactorComponent.TotalControlRods)
            args.PushMarkup(Loc.GetString("fission-reactor-examine-rods-wrench"));

        if (args.IsInDetailsRange)
            args.PushMarkup(Loc.GetString("fission-reactor-examine-lore"));
    }

    public static float GetIntegrity(FissionReactorComponent reactor)
    {
        var integrity = reactor.Damage / FissionReactorComponent.MeltdownPoint;
        integrity = MathF.Round((100 - integrity * 100) * 100) / 100;
        return MathF.Max(integrity, 0);
    }

    /// <summary>Статус для консоли и предупреждений (REACTOR_*).</summary>
    private int GetStatus(FissionReactorComponent reactor)
    {
        var integrity = GetIntegrity(reactor);
        if (integrity < FissionReactorComponent.MeltdownPercent)
            return 6;
        if (integrity < FissionReactorComponent.EmergencyPercent)
            return 5;
        if (integrity < FissionReactorComponent.DangerPercent)
            return 4;
        if (integrity < FissionReactorComponent.WarningPercent || reactor.Air.Temperature > FissionReactorComponent.CriticalTemperature)
            return 3;
        if (reactor.Air.Temperature > reactor.HeatDamageThreshold * 0.9f)
            return 2;
        if (reactor.Offline)
            return 0;
        return 1;
    }

    #region process()

    private void ProcessReactor(Entity<FissionReactorComponent> ent)
    {
        var reactor = ent.Comp;
        if (reactor.Broken)
        {
            // radiation_pulse(src, 6, chance = 50) у разрушенного ядра.
            if (_random.Prob(0.5f))
                SpawnRadiationPulse(ent, 6);
            return;
        }

        if (reactor.AdminIntervention)
            return;

        UpdateThresholds(reactor);

        if (!reactor.Offline && !reactor.StartingUp)
        {
            var range = Math.Clamp(reactor.FinalPower / 50000f, 2, 30);
            _light.SetRadius(ent, range);
            _light.SetEnergy(ent, MathF.Min(MathF.Max(reactor.Reactivity, 3), 10));
            _light.SetEnabled(ent, true);
        }
        else
        {
            _light.SetEnabled(ent, false);
        }

        if (reactor.ControlLockout)
            return;

        var minimumPower = 100 * (1 - reactor.ControlRodsRemaining / FissionReactorComponent.TotalControlRods);
        var effectiveControl = 1 + reactor.GasControlMod;
        if (reactor.OperatingPower < minimumPower)
        {
            reactor.OperatingPower = MathF.Min(minimumPower, reactor.OperatingPower + effectiveControl);
        }
        else if (reactor.DesiredPower > reactor.OperatingPower)
        {
            reactor.OperatingPower = MathF.Min(reactor.DesiredPower, reactor.OperatingPower + effectiveControl);
        }
        else if (reactor.DesiredPower < reactor.OperatingPower)
        {
            reactor.OperatingPower = MathF.Max(reactor.DesiredPower, reactor.OperatingPower - effectiveControl);
        }

        if (reactor.OperatingPower == reactor.DesiredPower && reactor.DesiredPower == 0 && !reactor.Offline)
            ShutOff(ent);

        if (reactor.OperatingPower > 0 && reactor.Offline)
            BootUp(ent);

        if (reactor.OperatingPower >= 10 && reactor.StartingUp)
            BecomeOperational(ent);

        if (reactor.Offline || reactor.StartingUp)
        {
            UpdateRadiation(ent, false);
            return;
        }

        if (reactor.SafetyOverride)
        {
            if (reactor.OperatingPower >= 100)
            {
                reactor.SendMessage = false;
                StartCountdown(ent);
                return;
            }

            // Немного «подыгрываем»: реактор раскаляется сам по себе.
            var temp = reactor.Air.Temperature;
            if (_atmos.GetHeatCapacity(reactor.Air, true) > 0 && temp < FissionReactorComponent.CriticalTemperature)
                reactor.Air.Temperature = temp + _random.Next(20, 201);
            else
                reactor.Air.Temperature = FissionReactorComponent.CriticalTemperature;

            if (reactor.Reactivity < 20)
                reactor.Reactivity += _random.Next(15, 41) / 100f;
            else
                reactor.Reactivity = 20;

            reactor.Damage += MathF.Max(0.5f, _random.Next(1, (int) (FissionReactorComponent.DamageMaximum * 20) + 1) / 20f);
            UpdateRadiation(ent, true);
            return;
        }

        reactor.FinalPower = 0;
        reactor.FinalHeat = 0;

        CalculateGasEffects(reactor);

        // Износ стержней: 1 / (1 + 2.5^(-0.077 * (x - 65))), полный износ от 90 %.
        const float algorithmDecay = 0.077f;
        var durabilityLoss = 1f;
        if (reactor.OperatingPower <= 90)
            durabilityLoss = MathF.Round(1 / (1 + MathF.Pow(2.5f, -algorithmDecay * (reactor.OperatingPower - 65))), 2);
        var operatingRate = OperatingRate(reactor);

        float activeChambers = 0;
        foreach (var uid in reactor.ConnectedChambers)
        {
            if (!TryComp<FissionChamberComponent>(uid, out var chamber) || GetRod(uid) is not { } rod)
                continue;

            if (chamber.State == FissionChamberState.Down)
            {
                CalculateStats((uid, chamber), rod, operatingRate);
                activeChambers++;
            }
            else if (chamber.State == FissionChamberState.Up)
            {
                activeChambers++;
            }
        }

        if (activeChambers == 0)
            activeChambers = 0.5f;

        foreach (var uid in reactor.ConnectedChambers)
        {
            if (!TryComp<FissionChamberComponent>(uid, out var chamber) || GetRod(uid) is not { } rod)
                continue;
            if (chamber.State == FissionChamberState.Open)
                continue;

            float powerTotal = 0;
            var durabilityMod = DurabilityMod(rod.Comp);
            if (chamber.State == FissionChamberState.Down)
            {
                if (chamber.Operational)
                    powerTotal = chamber.PowerTotal * durabilityMod;

                if (rod.Comp.Category == FissionRodCategory.Fuel)
                {
                    if (Enrich(rod.Comp, chamber.PowerModTotal * operatingRate, chamber.HeatModTotal * operatingRate))
                    {
                        if (!chamber.Enriching)
                        {
                            chamber.Enriching = true;
                            UpdateChamberVisuals((uid, chamber));
                        }
                    }
                    else if (chamber.Enriching)
                    {
                        chamber.Enriching = false;
                        UpdateChamberVisuals((uid, chamber));
                    }
                }
            }

            reactor.FinalHeat += chamber.HeatTotal * durabilityMod;
            reactor.FinalPower += powerTotal;
            if (!rod.Comp.Infinite)
                rod.Comp.Durability -= durabilityLoss + reactor.GasDepletionMod;
        }

        reactor.AverageHeatgen = reactor.FinalHeat != 0 ? reactor.FinalHeat / activeChambers : 0.01f;

        // Коэффициент реактивности.
        var temperature = reactor.Air.Temperature;
        var totalMoles = reactor.Air.TotalMoles;
        if (temperature <= 0 || totalMoles <= 0)
            temperature = 0;

        if (reactor.AverageHeatgen > FissionReactorComponent.AverageHeatThreshold)
        {
            reactor.Reactivity = 1 + reactor.GasReactivityBonus +
                                 (reactor.AverageHeatgen - FissionReactorComponent.AverageHeatThreshold) / FissionReactorComponent.AverageHeatThreshold;
        }
        else
        {
            reactor.Reactivity = 1 + reactor.GasReactivityBonus;
        }

        if (temperature > FissionReactorComponent.TotalHeatThreshold)
        {
            // y = a + b * ln(x)
            const float offset = 1;
            const float curveIntensity = 3.5f;
            var heatComponent = (temperature - FissionReactorComponent.TotalHeatThreshold) / FissionReactorComponent.HeatConversionRatio;
            reactor.Reactivity += offset + curveIntensity * MathF.Log(heatComponent);
        }

        reactor.Reactivity = Math.Clamp(reactor.Reactivity, 1, FissionReactorComponent.ReactivityCoefficientCap);

        reactor.FinalPower += reactor.GasFuelPower;
        reactor.FinalHeat += reactor.GasFuelHeat;

        reactor.FinalHeat *= reactor.Reactivity * 2 * FissionReactorComponent.HeatModifier;
        reactor.FinalPower *= reactor.Reactivity;
        reactor.FinalPower = MathF.Max(reactor.FinalPower, 0);

        // Побочный продукт деления — водород.
        const float gasOffset = 1;
        const float gasCurveIntensity = 1.6f;
        var powerComponent = MathF.Max(reactor.FinalPower / 5_000_000f, 0.01f);
        var h2Amount = Math.Clamp(gasOffset + gasCurveIntensity * MathF.Log(powerComponent), 0, 30);
        var hydrogen = new GasMixture(reactor.Air.Volume) { Temperature = reactor.Air.Temperature };
        hydrogen.AdjustMoles(Gas.Hydrogen, Math.Clamp(h2Amount * reactor.Reactivity, 0.2f, 100));
        _atmos.Merge(reactor.Air, hydrogen);

        UpdateRadiation(ent, true);

        // Нагрев теплоносителя.
        var heatCapacity = _atmos.GetHeatCapacity(reactor.Air, true);
        var effectiveHeat = reactor.FinalHeat / reactor.GasAbsorptionEffectiveness;
        if (heatCapacity > 0)
        {
            if (temperature < reactor.MinimumOperatingTemp)
                reactor.Air.Temperature = MathF.Max(temperature + effectiveHeat / heatCapacity, temperature + 200);
            else if (temperature > FissionReactorComponent.HeatCap)
                reactor.Air.Temperature = temperature + _random.Next(3, 21);
            else
                reactor.Air.Temperature = MathF.Max(temperature + effectiveHeat / heatCapacity, temperature + 2);
        }

        temperature = reactor.Air.Temperature;
        CheckPressureHazard(ent);

        // Повреждения и аварийные события.
        float newDamage = 0;
        if (totalMoles <= 0)
        {
            newDamage += FissionReactorComponent.DamageMaximum;
        }
        else
        {
            if (totalMoles <= FissionReactorComponent.MolMinimum)
            {
                newDamage += MathF.Max((1 - totalMoles / FissionReactorComponent.MolMinimum) * FissionReactorComponent.DamageMaximum,
                    FissionReactorComponent.DamageMinimum);
            }

            if (CheckOverheating(reactor))
            {
                // Y = (-A * B ^ -X) + A
                const float rateOfDecay = 1.13f;
                var damageIncrements = -((temperature - reactor.HeatDamageThreshold) / FissionReactorComponent.HeatDamageRate);
                var damageCalculation = -FissionReactorComponent.DamageMaximum * MathF.Pow(rateOfDecay, damageIncrements) +
                                        FissionReactorComponent.DamageMaximum;
                newDamage += MathF.Max(damageCalculation, FissionReactorComponent.DamageMinimum);

                if (reactor.Air.Pressure > FissionReactorComponent.PressureMaximum)
                    newDamage += FissionReactorComponent.PressureDamage;

                newDamage = Math.Clamp(newDamage, FissionReactorComponent.DamageMinimum, FissionReactorComponent.DamageMaximum);
                var damageMultiplier = Math.Clamp(1 + (50 - GetIntegrity(reactor)) / 25, 1, 3);
                damageMultiplier *= FissionReactorComponent.EventModifier * reactor.GasEventModifier;
                if (reactor.FinalCountdown)
                    damageMultiplier = 10;

                RollEvents(ent, newDamage, damageMultiplier);
            }
        }

        if (reactor.Damage > FissionReactorComponent.WarningPoint &&
            _timing.CurTime - reactor.LastWarning >= FissionReactorComponent.WarningDelay &&
            reactor.SendMessage && !reactor.FinalCountdown)
        {
            TryAlarm(ent, newDamage);
        }

        if (newDamage > 0)
        {
            AdjustDamage(ent, newDamage);
            reactor.SendMessage = true;
        }

        if (reactor.Damage >= FissionReactorComponent.MeltdownPoint)
        {
            reactor.SendMessage = false;
            StartCountdown(ent);
        }
    }

    private static float OperatingRate(FissionReactorComponent reactor) => reactor.OperatingPower / 100;

    /// <summary>
    /// update_minimum_temp и update_overheat_threshold: в SS13 пороги правятся инкрементально при подъёме/опускании
    /// камер, здесь пересчитываются по опущенным камерам каждый цикл.
    /// </summary>
    private void UpdateThresholds(FissionReactorComponent reactor)
    {
        float minimum = 0;
        float overheat = 0;
        foreach (var uid in reactor.ConnectedChambers)
        {
            if (!TryComp<FissionChamberComponent>(uid, out var chamber) || chamber.State != FissionChamberState.Down)
                continue;
            if (GetRod(uid) is not { } rod)
                continue;

            minimum = MathF.Max(minimum, rod.Comp.MinimumTempModifier);
            overheat += rod.Comp.ReactorOverheatModifier;
        }

        reactor.MinimumOperatingTemp = minimum;
        reactor.HeatDamageThreshold = 1000 + overheat + reactor.GasOverheatBonus;
    }

    private void UpdateRadiation(Entity<FissionReactorComponent> ent, bool active)
    {
        if (!active)
        {
            _radiation.SetSourceEnabled(ent.Owner, false);
            return;
        }

        // radiation_pulse(src, 6 * mult, 1.2 / mult, chance = 10 * mult)
        var multiplier = ent.Comp.Reactivity * ent.Comp.GasRadiationMod;
        _radiation.SetIntensity(ent.Owner, Math.Clamp(0.5f * multiplier, 0.5f, 50));
        _radiation.SetSourceEnabled(ent.Owner, true);
    }

    private void RollEvents(Entity<FissionReactorComponent> ent, float newDamage, float damageMultiplier)
    {
        var reactor = ent.Comp;

        // Выброс стержня охладителя.
        if (_random.Prob(Math.Clamp(newDamage * damageMultiplier * 0.3f / 100, 0, 1)))
        {
            var coolers = new List<Entity<FissionChamberComponent>>();
            foreach (var uid in reactor.ConnectedChambers)
            {
                if (TryComp<FissionChamberComponent>(uid, out var chamber) && chamber.State == FissionChamberState.Down &&
                    GetRod(uid) is { Comp.Category: FissionRodCategory.Coolant })
                {
                    coolers.Add((uid, chamber));
                }
            }

            if (coolers.Count > 0)
            {
                var failure = _random.Pick(coolers);
                if (_random.Prob(0.6f) || !failure.Comp.Welded)
                    EjectRod(failure);
            }
        }

        // Заваривает камеру.
        if (_random.Prob(Math.Clamp(newDamage * damageMultiplier * 3 / 100, 0, 1)))
        {
            var valid = new List<Entity<FissionChamberComponent>>();
            foreach (var uid in reactor.ConnectedChambers)
            {
                if (TryComp<FissionChamberComponent>(uid, out var chamber) && chamber.State == FissionChamberState.Down && !chamber.Welded)
                    valid.Add((uid, chamber));
            }

            if (valid.Count > 0)
                WeldShut(_random.Pick(valid));
        }

        // Отказ управляющего стержня.
        if (_random.Prob(Math.Clamp(newDamage * damageMultiplier * 0.5f / 100, 0, 1)) && reactor.ControlRodsRemaining > 0)
            ControlRodFailure(ent);

        // Заклинило аварийный клапан.
        if (_random.Prob(Math.Clamp(newDamage * damageMultiplier * 0.05f / 100, 0, 1)))
            BeginVenting(ent);
    }

    private bool CheckOverheating(FissionReactorComponent reactor)
    {
        if (reactor.Air.TotalMoles <= 0)
            return true;

        return reactor.Air.Pressure > FissionReactorComponent.PressureMaximum || reactor.Air.Temperature >= reactor.HeatDamageThreshold;
    }

    private void CheckPressureHazard(Entity<FissionReactorComponent> ent)
    {
        var reactor = ent.Comp;
        if (reactor.Broken)
            return;

        var pressure = reactor.Air.Pressure;
        if (pressure >= reactor.PressureCriticalThreshold)
        {
            var pressureDamage = MathF.Min(pressure / 100, FissionReactorComponent.MeltdownPoint / 45);
            AdjustDamage(ent, pressureDamage * reactor.PressureDamageRate);
            _audio.PlayPvs(reactor.SteamSound, ent, AudioParams.Default.WithVolume(4));

            if (_atmos.GetContainingMixture(ent.Owner, false, true) is { } tileAir)
            {
                var leak = new GasMixture { Temperature = reactor.Air.Temperature };
                leak.AdjustMoles(Gas.WaterVapor, pressure / 100);
                _atmos.Merge(tileAir, leak);
            }

            return;
        }

        if (pressure >= reactor.PressureWarningThreshold && _random.Prob(0.2f))
            _audio.PlayPvs(reactor.HissSound, ent);
    }

    private void TryAlarm(Entity<FissionReactorComponent> ent, float newDamage)
    {
        var reactor = ent.Comp;
        reactor.LastWarning = _timing.CurTime;
        var integrity = GetIntegrity(reactor).ToString("0.##");
        if (newDamage <= 0)
        {
            Announce(ent, Loc.GetString("fission-alert-safe", ("integrity", integrity)));
            reactor.SendMessage = false;
            return;
        }

        switch (GetStatus(reactor))
        {
            case 3:
            case 4:
                Announce(ent, Loc.GetString("fission-alert-warning", ("integrity", integrity)));
                break;
            case 5:
                Announce(ent, Loc.GetString("fission-alert-warning", ("integrity", integrity)), true);
                break;
            case 6:
                Announce(ent, Loc.GetString("fission-alert-emergency", ("integrity", integrity)), true);
                break;
        }
    }

    #endregion

    #region Газы

    private void CalculateGasEffects(FissionReactorComponent reactor)
    {
        reactor.HeatDamageThreshold -= reactor.GasOverheatBonus;
        reactor.GasControlMod = 0;
        reactor.GasRadiationMod = 1;
        reactor.GasPermeabilityMod = 0;
        reactor.GasPowerMod = 1;
        reactor.GasReactivityBonus = 0;
        reactor.GasEventModifier = 0;
        reactor.GasOverheatBonus = 0;
        reactor.GasDepletionMod = 0;
        reactor.GasIsFueled = false;
        reactor.GasFuelPower = 0;
        reactor.GasFuelMoles = 0;
        reactor.GasFuelHeat = 0;
        reactor.GasAbsorptionEffectiveness = reactor.GasAbsorptionConstant;

        ProcessCoolantGases(reactor);
        ProcessModeratorGases(reactor);

        reactor.GasControlMod = Math.Clamp(reactor.GasControlMod, 0, 5);
        reactor.GasRadiationMod = Math.Clamp(reactor.GasRadiationMod, 1, 10);
        reactor.GasPermeabilityMod = Math.Clamp(reactor.GasPermeabilityMod, 0, 10);
        reactor.GasDepletionMod = Math.Clamp(reactor.GasDepletionMod, 0, 5);
        reactor.GasEventModifier /= 100;
        reactor.GasEventModifier = 1 - reactor.GasEventModifier;
        reactor.GasAbsorptionEffectiveness = reactor.GasAbsorptionConstant + reactor.GasPermeabilityMod;
        reactor.HeatDamageThreshold += reactor.GasOverheatBonus;
    }

    private void ProcessCoolantGases(FissionReactorComponent reactor)
    {
        var totalMoles = reactor.Air.TotalMoles;
        if (totalMoles <= 0)
            return;

        var moleMultiplier = 1f;
        if (totalMoles > FissionReactorComponent.MoleBonusThreshold)
        {
            const float offset = 1;
            const float curveIntensity = 0.7f;
            var gasComponent = MathF.Max((totalMoles - FissionReactorComponent.MoleBonusThreshold) / FissionReactorComponent.MoleBonusComponent, 0.01f);
            moleMultiplier = MathF.Max(offset + curveIntensity * MathF.Log(gasComponent), 0);
        }

        foreach (var (gas, effect) in CoolantEffects)
        {
            var moles = reactor.Air.GetMoles(gas);
            if (moles <= 0)
                continue;

            var fraction = moles / totalMoles;
            reactor.GasOverheatBonus += effect.Overheat * fraction * moleMultiplier;
            reactor.GasEventModifier += effect.Event * fraction;
            reactor.GasReactivityBonus += effect.Reactivity * fraction * moleMultiplier;
        }
    }

    private void ProcessModeratorGases(FissionReactorComponent reactor)
    {
        var moderator = reactor.ModeratorGas;
        var totalMoles = moderator.TotalMoles;
        if (totalMoles < FissionReactorComponent.MolMinimum)
            return;

        foreach (var (gas, effect) in ModeratorEffects)
        {
            var moles = moderator.GetMoles(gas);
            if (moles <= 0)
                continue;

            var fraction = moles / totalMoles;
            reactor.GasControlMod += effect.Control * fraction;
            reactor.GasRadiationMod += effect.Radiation * fraction;
            reactor.GasPermeabilityMod += effect.Permeability * fraction;
            reactor.GasDepletionMod += effect.Depletion * fraction;
            reactor.GasFuelHeat += effect.HeatPerMole * moles;

            if (effect.PowerPerMole > 0)
            {
                reactor.GasFuelPower += moles * effect.PowerPerMole;
                reactor.GasFuelMoles += moles;
                reactor.GasIsFueled = true;
            }

            if (effect.PowerMod * fraction > reactor.GasPowerMod)
                reactor.GasPowerMod = effect.PowerMod * fraction;
        }

        if (reactor.GasIsFueled && reactor.GasFuelMoles > 0)
        {
            reactor.GasFuelPower *= reactor.GasPowerMod * 5;
            if (reactor.OperatingPower >= 20 && _random.Prob(0.3f))
                reactor.Air.AdjustMoles(Gas.NitrousOxide, reactor.GasFuelMoles / 50);
        }

        moderator.Clear();
    }

    #endregion

    #region Включение и выключение

    private void ShutOff(Entity<FissionReactorComponent> ent)
    {
        var reactor = ent.Comp;
        reactor.StartingUp = true;
        reactor.Offline = true;
        reactor.CanCreatePower = false;
        reactor.FinalHeat = 0;
        reactor.FinalPower = 0;
        reactor.Reactivity = 1;
        _light.SetEnabled(ent, false);
        _audio.PlayPvs(reactor.ShutoffSound, ent, AudioParams.Default.WithMaxDistance(15));
        _ambient.SetAmbience(ent, false);
        UpdateRadiation(ent, false);

        if (!reactor.SendMessage)
            return;

        Announce(ent, Loc.GetString("fission-alert-scram", ("integrity", GetIntegrity(reactor).ToString("0.##"))));
        reactor.SendMessage = false;
    }

    private void BootUp(Entity<FissionReactorComponent> ent)
    {
        ent.Comp.Offline = false;
        _audio.PlayPvs(ent.Comp.StartupBeginningSound, ent, AudioParams.Default.WithVolume(-2));
        _ambient.SetSound(ent, ent.Comp.StartupLoopSound);
        _ambient.SetAmbience(ent, true);
    }

    private void BecomeOperational(Entity<FissionReactorComponent> ent)
    {
        var reactor = ent.Comp;
        reactor.StartingUp = false;
        reactor.Offline = false;
        reactor.CanCreatePower = true;
        _audio.PlayPvs(reactor.StartupSound, ent, AudioParams.Default.WithMaxDistance(15));
        _ambient.SetSound(ent, reactor.LoopSound);
        _ambient.SetAmbience(ent, true);
        _light.SetRadius(ent, 2);
        _light.SetEnabled(ent, true);
    }

    /// <summary>scram(): аварийная остановка при перегрузке ЦК.</summary>
    private void Scram(Entity<FissionReactorComponent> ent)
    {
        var reactor = ent.Comp;
        reactor.Reactivity = 1;
        reactor.Offline = true;
        reactor.StartingUp = true;
        reactor.FinalHeat = 0;
        reactor.OperatingPower = 0;
        reactor.FinalPower = 0;
        if (reactor.Air.Temperature > 300)
            reactor.Air.Temperature = 300;
        _ambient.SetAmbience(ent, false);
        UpdateRadiation(ent, false);
    }

    #endregion

    #region Аварийные события

    private void ControlRodFailure(Entity<FissionReactorComponent> ent)
    {
        if (ent.Comp.ControlRodsRemaining <= 0)
            return;

        _audio.PlayPvs(ent.Comp.ControlRodFailSound, ent);
        ent.Comp.ControlRodsRemaining--;
        Announce(ent, Loc.GetString("fission-alert-control-rod", ("rods", ent.Comp.ControlRodsRemaining)));
    }

    private void BeginVenting(Entity<FissionReactorComponent> ent)
    {
        if (ent.Comp.Venting)
            return;

        SpawnRadiationPulse(ent, 4);
        SpawnSmoke(ent);
        ent.Comp.Venting = true;
        ent.Comp.VentLockout = true;
    }

    /// <summary>process_atmos при открытом клапане: выравнивание давления с окружающим воздухом.</summary>
    private void ProcessVenting(Entity<FissionReactorComponent> ent)
    {
        var air = ent.Comp.Air;
        if (_atmos.GetContainingMixture(ent.Owner, false, true) is not { } environment)
            return;

        var pressureDelta = (air.Pressure - environment.Pressure) / 10;
        if (MathF.Abs(pressureDelta) < 0.01f)
            return;

        if (pressureDelta > 0)
        {
            if (air.TotalMoles <= 0 || air.Temperature <= 0)
                return;

            var transfer = MathF.Min(pressureDelta * air.Volume / (air.Temperature * Atmospherics.R), air.Volume);
            _atmos.Merge(environment, air.Remove(transfer));
            return;
        }

        pressureDelta = -pressureDelta;
        if (environment.TotalMoles <= 0 || environment.Temperature <= 0)
            return;

        var moles = MathF.Min(pressureDelta * air.Volume / (environment.Temperature * Atmospherics.R), air.Volume);
        _atmos.Merge(air, environment.Remove(moles));
    }

    #endregion

    #region Повреждения, отсчёт и расплав

    private void AdjustDamage(Entity<FissionReactorComponent> ent, float damage, bool setByNumber = false)
    {
        var reactor = ent.Comp;
        reactor.Damage = Math.Clamp(setByNumber ? damage : reactor.Damage + damage, 0, FissionReactorComponent.MeltdownPoint);
        if (reactor.Damage >= FissionReactorComponent.MeltdownPoint && reactor.Offline)
            SetBroken(ent, false);
    }

    private void StartCountdown(Entity<FissionReactorComponent> ent)
    {
        var reactor = ent.Comp;
        if (reactor.FinalCountdown || reactor.Broken)
            return;

        reactor.FinalCountdown = true;
        reactor.CountdownRemaining = FissionReactorComponent.CountdownSeconds;
        reactor.NextCountdownStep = _timing.CurTime;
        PlayGlobalOnMap(ent, reactor.AlertSound);
        Announce(ent, Loc.GetString("fission-alert-countdown-start"), true);
    }

    private void CountdownStep(Entity<FissionReactorComponent> ent)
    {
        var reactor = ent.Comp;
        reactor.NextCountdownStep = _timing.CurTime + TimeSpan.FromSeconds(1);

        if (reactor.AdminIntervention)
        {
            reactor.FinalCountdown = false;
            AdjustDamage(ent, FissionReactorComponent.MeltdownPoint - 1, true);
            return;
        }

        if (reactor.Offline)
        {
            Announce(ent, Loc.GetString("fission-alert-averted"), true);
            reactor.FinalCountdown = false;
            return;
        }

        var seconds = reactor.CountdownRemaining;
        if (seconds < 0)
        {
            reactor.FinalCountdown = false;
            SetBroken(ent);
            return;
        }

        reactor.CountdownRemaining--;
        if (seconds % 5 != 0 && seconds > 5)
            return;

        Announce(ent, seconds > 5
            ? Loc.GetString("fission-alert-countdown", ("seconds", seconds))
            : Loc.GetString("fission-alert-countdown-final", ("seconds", seconds)), true);
    }

    private void SetBroken(Entity<FissionReactorComponent> ent, bool meltdown = true)
    {
        var reactor = ent.Comp;
        if (reactor.Broken)
            return;

        reactor.Broken = true;
        reactor.FinalCountdown = false;

        if (reactor.SafetyOverride && reactor.OperatingPower >= 100)
        {
            FinalizeOverload(ent);
            return;
        }

        RebuildNetwork(ent);
        reactor.CanCreatePower = false;
        reactor.FinalPower = 0;
        _ambient.SetAmbience(ent, false);
        _light.SetEnabled(ent, false);
        if (meltdown)
            Blowout(ent);

        UpdateReactorVisuals(ent);
    }

    private void SetFixed(Entity<FissionReactorComponent> ent)
    {
        ent.Comp.Broken = false;
        ent.Comp.RepairStep = FissionRepairStep.Digging;
        ent.Comp.Offline = true;
        ent.Comp.StartingUp = true;
        ent.Comp.DesiredPower = 0;
        ent.Comp.OperatingPower = 0;
        _radiation.SetSourceEnabled(ent.Owner, false);
        RebuildNetwork(ent);
        UpdateReactorVisuals(ent);
    }

    /// <summary>blowout(): максимальный взрыв, затем расплав и радиоактивные осадки.</summary>
    private void Blowout(Entity<FissionReactorComponent> ent)
    {
        var coords = _xform.GetMapCoordinates(ent);
        // GLOB.max_ex_*_range Paradise: 3 / 7 / 14.
        QueueSs13Explosion(coords, 3, 7, 14, ent);
        Meltdown(ent);
        ent.Comp.FinalPower = 0;

        // SSweather.run_weather(/datum/weather/rad_storm/nuclear_fallout)
        var duration = TimeSpan.FromSeconds(_random.Next(60, 151));
        _weather.TryAddWeather(coords.MapId, ent.Comp.Fallout, out _, duration + TimeSpan.FromSeconds(5));
        StartFallout(Transform(ent).GridUid, coords.MapId, duration);
    }

    private void Meltdown(Entity<FissionReactorComponent> ent)
    {
        var reactor = ent.Comp;
        reactor.ActiveMeltdown = true;
        UpdateReactorVisuals(ent);
        Robust.Shared.Timing.Timer.Spawn(TimeSpan.FromSeconds(5), () =>
        {
            if (TryComp<FissionReactorComponent>(ent, out var comp))
            {
                comp.ActiveMeltdown = false;
                UpdateReactorVisuals((ent, comp));
            }
        });

        SpawnRadiationPulse(ent, 40);
        SpawnWaste(ent, 10);
        PlayGlobalOnMap(ent, reactor.MeltdownSound, Loc.GetString("fission-meltdown-hiss"));

        // Перегрузка освещения на станции: 70 % ламп.
        if (Transform(ent).GridUid is { } grid)
        {
            var lights = EntityQueryEnumerator<PoweredLightComponent, TransformComponent>();
            while (lights.MoveNext(out var light, out var lightComp, out var lightXform))
            {
                if (lightXform.GridUid == grid && _random.Prob(0.7f))
                    _poweredLight.TryDestroyBulb(light, lightComp);
            }
        }

        if (_atmos.GetContainingMixture(ent.Owner, false, true) is { } tileAir)
        {
            reactor.ModeratorGas.Temperature = reactor.Air.Temperature;
            _atmos.Merge(tileAir, reactor.Air);
            _atmos.Merge(tileAir, reactor.ModeratorGas);
            reactor.Air.Clear();
            reactor.ModeratorGas.Clear();
        }

        reactor.FinalPower = 0;
        var modifier = Math.Clamp(reactor.Reactivity * FissionReactorComponent.ExplosionModifier, 8, 40);
        var coords = _xform.GetMapCoordinates(ent);
        QueueSs13Explosion(coords, modifier / 2, modifier, modifier + 3, ent);
        _emp.EmpPulse(coords, 15, 100000, TimeSpan.FromSeconds(60));
    }

    /// <summary>Пересчёт радиусов SS13 (devastation/heavy/light) в интенсивность взрыва SS14.</summary>
    private void QueueSs13Explosion(MapCoordinates coords, float devastation, float heavy, float light, EntityUid cause)
    {
        const float slope = 2f;
        var radius = MathF.Max(light, MathF.Max(heavy, devastation));
        var maxIntensity = 5 + heavy * 5 + devastation * 10;
        _explosion.QueueExplosion(coords, "Default", _explosion.RadiusToIntensity(radius, slope, maxIntensity),
            slope, maxIntensity, cause);
    }

    /// <summary>nuclear_waste_spawner/fire(): эпицентр и 35 % клеток в радиусе.</summary>
    private void SpawnWaste(EntityUid source, int range)
    {
        if (!TryGetTile(source, out var grid, out var center))
            return;

        Spawn(Comp<FissionReactorComponent>(source).WasteEpicenter, _map.GridTileToLocal(grid.Value, grid.Value, center));
        for (var dx = -range; dx <= range; dx++)
        {
            for (var dy = -range; dy <= range; dy++)
            {
                if (dx == 0 && dy == 0 || !_random.Prob(0.35f))
                    continue;

                var tile = center + new Vector2i(dx, dy);
                if (!_map.TryGetTileRef(grid.Value, grid.Value, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                    continue;

                Spawn(Comp<FissionReactorComponent>(source).Waste, _map.GridTileToLocal(grid.Value, grid.Value, tile));
            }
        }
    }

    #endregion

    #region Перегрузка ЦК (Delta)

    /// <summary>overload_reactor(): код «Дельта», через 5 секунд — подготовка к ручному подрыву.</summary>
    public bool OverloadReactor(Entity<FissionReactorComponent> ent)
    {
        if (ent.Comp.SafetyOverride || ent.Comp.OverloadPrepAt != null)
            return false;

        if (_stationSystem.GetOwningStation(ent) is { } station)
            _alertLevel.SetLevel(station, "delta", true, true, true);

        ent.Comp.OverloadPrepAt = _timing.CurTime + TimeSpan.FromSeconds(5);
        return true;
    }

    private void PrepOverload(Entity<FissionReactorComponent> ent)
    {
        _chat.DispatchGlobalAnnouncement(Loc.GetString("fission-overload-announcement"),
            Loc.GetString("fission-overload-sender"), true, null, Color.Red);

        var reactor = ent.Comp;
        reactor.DesiredPower = 0;
        Scram(ent);
        reactor.ControlLockout = true;
        reactor.SafetyOverride = true;
        foreach (var uid in reactor.ConnectedChambers)
        {
            if (TryComp<FissionChamberComponent>(uid, out var chamber))
                SetIdleOverload((uid, chamber));
        }
    }

    private bool CheckOverloadReady(FissionReactorComponent reactor)
    {
        if (reactor.ConnectedChambers.Count < FissionReactorComponent.MinChambersToOverload)
            return false;

        foreach (var uid in reactor.ConnectedChambers)
        {
            if (!TryComp<FissionChamberComponent>(uid, out var chamber) || chamber.State != FissionChamberState.OverloadIdle)
                return false;
            if (GetRod(uid) is not { Comp.Category: FissionRodCategory.Fuel })
                return false;
        }

        return true;
    }

    private void SetOverload(Entity<FissionReactorComponent> ent)
    {
        ent.Comp.ControlLockout = false;
        foreach (var uid in ent.Comp.ConnectedChambers)
        {
            if (TryComp<FissionChamberComponent>(uid, out var chamber))
                SetActiveOverload((uid, chamber));
        }
    }

    /// <summary>finalize_overload(): тревога, через 10 секунд станция уничтожена.</summary>
    private void FinalizeOverload(Entity<FissionReactorComponent> ent)
    {
        ent.Comp.ActiveMeltdown = true;
        UpdateReactorVisuals(ent);
        _audio.PlayPvs(ent.Comp.AlarmSound, ent, AudioParams.Default.WithVolume(6).WithMaxDistance(30));
        ent.Comp.OverloadDetonateAt = _timing.CurTime + TimeSpan.FromSeconds(10);
    }

    private void DetonateOverload(Entity<FissionReactorComponent> ent)
    {
        ent.Comp.ActiveMeltdown = false;
        UpdateReactorVisuals(ent);
        _chat.DispatchGlobalAnnouncement(Loc.GetString("fission-overload-destroyed"), Loc.GetString("fission-overload-sender"),
            true, null, Color.Red);

        // Взрыв уровня ядерной боеголовки и конец раунда.
        var coords = _xform.GetMapCoordinates(ent);
        _explosion.QueueExplosion(coords, "Default", 2000000, 5, 150, ent, maxTileBreak: int.MaxValue);
        _roundEnd.EndRound(TimeSpan.FromSeconds(30));
    }

    #endregion

    #region Визуал

    private void UpdateReactorVisuals(Entity<FissionReactorComponent> ent)
    {
        var reactor = ent.Comp;
        FissionReactorState state;
        if (reactor.ActiveMeltdown)
            state = FissionReactorState.Meltdown;
        else if (reactor.Broken)
            state = reactor.RepairStep >= FissionRepairStep.Wrench ? FissionReactorState.Maintenance : FissionReactorState.Broken;
        else if (!reactor.Offline && reactor.StartingUp)
            state = FissionReactorState.Starting;
        else if (reactor.Offline)
            state = FissionReactorState.Off;
        else if (reactor.Air.Temperature > reactor.HeatDamageThreshold * 0.9f || reactor.SafetyOverride)
            state = FissionReactorState.Overheat;
        else if (reactor.Air.Temperature > reactor.HeatDamageThreshold * 0.5f)
            state = FissionReactorState.Hot;
        else
            state = FissionReactorState.On;

        _appearance.SetData(ent, FissionReactorVisuals.State, state);

        // rods_[remaining]_[state]: чем глубже стержни, тем выше индекс.
        var rodState = Math.Clamp((int) MathF.Round((100 - reactor.OperatingPower) / 20), 0, 5);
        _appearance.SetData(ent, FissionReactorVisuals.Rods, reactor.Broken ? -1 : rodState);
    }

    #endregion
}
