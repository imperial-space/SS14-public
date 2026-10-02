using System.Linq;
using System.Numerics;
using Content.Shared.Atmos;
using Content.Shared.Emp;
using Content.Shared.Gravity;
using Content.Shared.Imperial.Hypertorus;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Hypertorus;

/// <summary>Урон и лечение ядра, опасные эффекты, тревоги и авария (hfr_procs.dm, hfr_main_processes.dm).</summary>
public sealed partial class HypertorusSystem
{
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;

    /// <summary>Пауза между сообщениями о повреждении (WARNING_TIME_DELAY).</summary>
    private static readonly TimeSpan WarningTimeDelay = TimeSpan.FromSeconds(60);

    /// <summary>HYPERTORUS_ACCENT_SOUND_MIN_COOLDOWN.</summary>
    private static readonly TimeSpan AccentSoundMinCooldown = TimeSpan.FromSeconds(3);

    /// <summary>HYPERTORUS_COUNTDOWN_TIME, в десятых секунды.</summary>
    private const int CountdownTenths = 300;

    private const string HallucinationEffect = "StatusEffectSeeingRainbow";

    #region Урон и лечение

    /// <summary>process_damageheal: перегруз массой, лечение малой массой и холодом, железо, гиперкритическая масса.</summary>
    private void ProcessDamageHeal(Entity<HypertorusCoreComponent> ent, float secondsPerTick)
    {
        var core = ent.Comp;
        var fusionMoles = core.InternalFusion.TotalMoles;
        core.CriticalThresholdProximityArchived = core.CriticalThresholdProximity;
        core.WarningDamageFlags &= HypertorusDamageFlags.Emped;

        if (core.PowerLevel >= OverfullMinPowerLevel)
        {
            var overfull = OverfullMolarSlope * fusionMoles + OverfullTemperatureSlope * core.CoolantTemperature + OverfullConstant;
            core.CriticalThresholdProximity = (float) Math.Max(core.CriticalThresholdProximity + Math.Max(overfull * secondsPerTick, 0), 0);
            core.WarningDamageFlags |= HypertorusDamageFlags.HighPowerDamage;
        }

        if (fusionMoles < SubcriticalMoles && core.PowerLevel <= 5)
        {
            var restore = (fusionMoles - SubcriticalMoles) / SubcriticalScale;
            core.CriticalThresholdProximity = Math.Max(core.CriticalThresholdProximity + Math.Min(restore * secondsPerTick, 0), 0);
        }

        var coolantMoles = GetPipeAir(ent)?.TotalMoles ?? 0;
        if (fusionMoles > 0 && coolantMoles > 0 && core.CoolantTemperature < ColdCoolantThreshold && core.PowerLevel <= 4)
        {
            var restore = Math.Log10(Math.Max(core.CoolantTemperature, 1) * ColdCoolantScale) - ColdCoolantMaxRestore * 2;
            core.CriticalThresholdProximity = (float) Math.Max(core.CriticalThresholdProximity + Math.Min(restore * secondsPerTick, 0), 0);
        }

        core.CriticalThresholdProximity += Math.Max(core.IronContent - MaxSafeIron, 0) * secondsPerTick;
        if (core.IronContent - MaxSafeIron > 0)
            core.WarningDamageFlags |= HypertorusDamageFlags.IronContentDamage;

        // Предел урона за тик.
        core.CriticalThresholdProximity = Math.Min(
            core.CriticalThresholdProximityArchived + secondsPerTick * DamageCapMultiplier * core.MeltingPoint,
            core.CriticalThresholdProximity);

        if (fusionMoles >= HypercriticalMoles)
        {
            var hypercritical = Math.Max((fusionMoles - HypercriticalMoles) * HypercriticalScale, 0);
            hypercritical = Math.Min(hypercritical, HypercriticalMaxDamage) * secondsPerTick;
            core.CriticalThresholdProximity = Math.Max(core.CriticalThresholdProximity + hypercritical, 0);
            core.WarningDamageFlags |= HypertorusDamageFlags.HighFuelMixMole;
        }

        if (core.PowerLevel > 4 && _random.Prob(Math.Min(1f, IronChancePerFusionLevel * core.PowerLevel / 100f)))
        {
            core.IronContent += IronAccumulatedPerSecond * secondsPerTick;
            core.WarningDamageFlags |= HypertorusDamageFlags.IronContentIncrease;
        }

        if (core.IronContent > 0 && core.PowerLevel <= 4 && _random.Prob(0.25f / (core.PowerLevel + 1)))
            core.IronContent = Math.Max(core.IronContent - 0.01f * secondsPerTick, 0);

        core.IronContent = Math.Clamp(core.IronContent, 0, 1);
    }

    /// <summary>process_moderator_overflow: при гиперкритической массе модератора трескается порт и газ вытекает.</summary>
    private void ProcessModeratorOverflow(Entity<HypertorusCoreComponent> ent, float secondsPerTick)
    {
        var core = ent.Comp;
        var moderator = core.ModeratorInternal;
        var pressure = moderator.Pressure;

        var cracked = MachineParts(core).FirstOrDefault(p => Comp<HypertorusPartComponent>(p).Cracked);
        if (cracked != default)
        {
            float leakRate;
            if (pressure < MediumSpillPressure)
            {
                if (!_random.Prob(WeakSpillChance / 100f))
                    return;
                leakRate = WeakSpillRate;
            }
            else
            {
                leakRate = pressure < StrongSpillPressure ? MediumSpillRate : StrongSpillRate;
            }

            SpillGases(cracked, moderator, core.ModeratorTemp, (float) (1 - Math.Pow(1 - leakRate, secondsPerTick)));
            return;
        }

        if (moderator.TotalMoles < HypercriticalMoles)
            return;

        var parts = MachineParts(core).ToList();
        if (parts.Count == 0)
            return;

        var part = _random.Pick(parts);
        Comp<HypertorusPartComponent>(part).Cracked = true;
        _appearance.SetData(part, HypertorusVisuals.Cracked, true);

        if (pressure < MediumSpillPressure)
            return;

        var coords = _xform.GetMapCoordinates(part);
        if (pressure < StrongSpillPressure)
        {
            QueueSs13Explosion(coords, 0, 0, 1, part);
            SpillGases(part, moderator, core.ModeratorTemp, MediumSpillInitial);
            return;
        }

        QueueSs13Explosion(coords, 0, 1, 3, part);
        SpillGases(part, moderator, core.ModeratorTemp, StrongSpillInitial);
    }

    private void SpillGases(EntityUid origin, GasMixture target, double temperature, float ratio)
    {
        var removed = RemoveRatioAt(target, temperature, ratio);
        if (_atmos.GetContainingMixture(origin, false, true) is { } air)
            _atmos.Merge(air, removed);
    }

    #endregion

    #region Опасные эффекты

    /// <summary>check_nuclear_particles: с 4 уровня BZ заставляет углы выстреливать ядерными частицами.</summary>
    private void CheckNuclearParticles(Entity<HypertorusCoreComponent> ent, Dictionary<Gas, float> moderatorList)
    {
        var core = ent.Comp;
        if (core.PowerLevel < 4 || moderatorList.GetValueOrDefault(Gas.BZ) < 150f / core.PowerLevel || core.Corners.Count == 0)
            return;

        var corner = _random.Pick(core.Corners);
        var from = _xform.GetMapCoordinates(corner);
        var direction = from.Position - _xform.GetMapCoordinates(ent).Position;
        if (direction == Vector2.Zero)
            return;

        var particle = Spawn(core.NuclearParticle, from);
        _gun.ShootProjectile(particle, Vector2.Normalize(direction), Vector2.Zero, ent, ent);
    }

    /// <summary>check_lightning_arcs: антиноблий или сильный урон рождают молнии с 4 уровня.</summary>
    private void CheckLightningArcs(Entity<HypertorusCoreComponent> ent, Dictionary<Gas, float> moderatorList)
    {
        var core = ent.Comp;
        if (core.PowerLevel < 4)
            return;

        if (moderatorList.GetValueOrDefault(Gas.AntiNoblium) <= 50 && core.CriticalThresholdProximity <= 500)
            return;

        var zaps = core.PowerLevel - 2;
        if (core.CriticalThresholdProximity > 650 && _random.Prob(0.2f))
            zaps += 1;

        var lightning = core.PowerLevel switch
        {
            5 => "SuperchargedLightning",
            6 => "HyperchargedLightning",
            _ => "Lightning",
        };

        _audio.PlayPvs(core.ZapSound, ent, AudioParams.Default.WithMaxDistance(17));
        _lightning.ShootRandomLightnings(ent, 5, zaps, lightning);
    }

    /// <summary>check_gravity_pulse: при повреждениях ядро притягивает живых на шаг к себе.</summary>
    private void CheckGravityPulse(Entity<HypertorusCoreComponent> ent, float secondsPerTick)
    {
        var core = ent.Comp;
        var chance = Math.Clamp(core.CriticalThresholdProximity / 15f, 0, 100) / 100f;
        if (chance <= 0 || !_random.Prob(1 - (float) Math.Pow(1 - chance, secondsPerTick)))
            return;

        var range = (int) Math.Round(Math.Log(core.CriticalThresholdProximity, 2.5));
        if (range <= 0)
            return;

        var center = _xform.GetMapCoordinates(ent);
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(center, range))
        {
            if (_mobState.IsDead(mob) || _gravity.IsWeightless(mob.Owner))
                continue;

            var direction = center.Position - _xform.GetMapCoordinates(mob).Position;
            if (direction.LengthSquared() < 0.25f)
                continue;

            _throwing.TryThrow(mob, Vector2.Normalize(direction), 5f, ent, pushbackRatio: 0, doSpin: false);
        }
    }

    /// <summary>visible_hallucination_pulse: BZ в модераторе вызывает галлюцинации у тех, кто рядом.</summary>
    private void HallucinationPulse(Entity<HypertorusCoreComponent> ent, float secondsPerTick)
    {
        var core = ent.Comp;
        var range = Math.Min(7, (int) Math.Round(Math.Pow(Math.Abs(core.HeatOutput), 0.25)));
        if (range <= 0)
            return;

        var duration = TimeSpan.FromSeconds(Math.Min(100 * core.PowerLevel * secondsPerTick, 300));
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(_xform.GetMapCoordinates(ent), range))
        {
            if (!_mobState.IsDead(mob))
                _statusEffects.TryAddStatusEffectDuration(mob, HallucinationEffect, duration);
        }
    }

    /// <summary>play_ambience: акценты суперматерии и гул, громче с уровнем синтеза.</summary>
    private void PlayAmbience(Entity<HypertorusCoreComponent> ent, float secondsPerTick)
    {
        var core = ent.Comp;
        var now = _timing.CurTime;
        if (core.LastAccentSound < now && _random.Prob(1 - (float) Math.Pow(0.9, secondsPerTick)))
        {
            var aggression = Math.Min(core.CriticalThresholdProximity / 800f * (core.PowerLevel / 5f), 1f) * 100;
            var volume = SharedAudioSystem.GainToVolume(Math.Max(50, aggression) / 100f);
            var sound = core.CriticalThresholdProximity >= 300 ? core.MeltingSound : core.CalmSound;
            var range = core.CriticalThresholdProximity >= 300 ? 40 : 25;
            _audio.PlayPvs(sound, ent, AudioParams.Default.WithVolume(volume).WithMaxDistance(range));
            var next = TimeSpan.FromSeconds(Math.Round((100 - aggression) * 5 + 5) / 10);
            core.LastAccentSound = now + (next > AccentSoundMinCooldown ? next : AccentSoundMinCooldown);
        }

        var fuel = core.SelectedFuel is { } fuelId ? _proto.Index(fuelId) : null;
        var hum = fuel != null && CheckFuel(core, fuel) ? core.PowerLevel + 1 : 1;
        _ambient.SetVolume(ent, SharedAudioSystem.GainToVolume(Math.Clamp(hum * 8, 0, 50) / 50f));
    }

    #endregion

    #region Тревоги и авария

    /// <summary>check_alert: сообщения в инженерный и общий каналы, звуки тревоги, обратный отсчёт.</summary>
    private void CheckAlert(Entity<HypertorusCoreComponent> ent)
    {
        var core = ent.Comp;
        if (core.CriticalThresholdProximity < core.WarningPoint)
            return;

        var now = _timing.CurTime;
        if (now - core.LastWarning >= WarningTimeDelay)
        {
            Alarm(ent);
            var integrity = GetIntegrityPercent(core);
            if (core.CriticalThresholdProximity > core.EmergencyPoint)
            {
                Radio(ent, Loc.GetString("hypertorus-emergency-alert", ("integrity", integrity)), core.CommonChannel);
                core.LastWarning = now;
                if (!core.HasReachedEmergency)
                    core.HasReachedEmergency = true;
                SendRadioExplanation(ent);
            }
            else if (core.CriticalThresholdProximity >= core.CriticalThresholdProximityArchived)
            {
                Radio(ent, Loc.GetString("hypertorus-warning-alert", ("integrity", integrity)), core.EngineeringChannel);
                core.LastWarning = now - WarningTimeDelay / 2;
                SendRadioExplanation(ent);
            }
            else
            {
                Radio(ent, Loc.GetString("hypertorus-safe-alert", ("integrity", integrity)), core.EngineeringChannel);
                core.LastWarning = now;
            }
        }

        if (core.CriticalThresholdProximity > core.MeltingPoint)
            StartCountdown(ent);
    }

    private void Radio(EntityUid core, string message, Robust.Shared.Prototypes.ProtoId<Content.Shared.Radio.RadioChannelPrototype> channel)
    {
        _radio.SendRadioMessage(core, message, channel, core);
    }

    /// <summary>alarm: звук по состоянию целостности.</summary>
    private void Alarm(Entity<HypertorusCoreComponent> ent)
    {
        var core = ent.Comp;
        var integrity = GetIntegrityPercent(core);
        var sound = integrity switch
        {
            < 5 => core.MeltingAlarm,
            < 25 => core.EmergencyAlarm,
            < 50 => core.DangerAlarm,
            < 100 => core.WarningAlarm,
            _ => null,
        };

        if (sound != null)
            _audio.PlayPvs(sound, ent, AudioParams.Default.WithMaxDistance(40));
    }

    /// <summary>send_radio_explanation: почему ядро разрушается; после ЭМИ — белиберда.</summary>
    private void SendRadioExplanation(Entity<HypertorusCoreComponent> ent)
    {
        var core = ent.Comp;
        if ((core.WarningDamageFlags & HypertorusDamageFlags.Emped) != 0)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789  ";
            var length = _random.Next(50, 71);
            var garbage = new string(Enumerable.Range(0, length).Select(_ => chars[_random.Next(chars.Length)]).ToArray());
            Radio(ent, garbage, core.EngineeringChannel);
            return;
        }

        if ((core.WarningDamageFlags & HypertorusDamageFlags.HighPowerDamage) != 0)
            Radio(ent, Loc.GetString("hypertorus-explain-high-power"), core.EngineeringChannel);
        if ((core.WarningDamageFlags & HypertorusDamageFlags.IronContentDamage) != 0)
            Radio(ent, Loc.GetString("hypertorus-explain-iron-damage"), core.EngineeringChannel);
        if ((core.WarningDamageFlags & HypertorusDamageFlags.HighFuelMixMole) != 0)
            Radio(ent, Loc.GetString("hypertorus-explain-fuel-moles"), core.EngineeringChannel);
        if ((core.WarningDamageFlags & HypertorusDamageFlags.IronContentIncrease) != 0)
            Radio(ent, Loc.GetString("hypertorus-explain-iron-increase"), core.EngineeringChannel);
    }

    private void OnCoreEmp(Entity<HypertorusCoreComponent> ent, ref EmpPulseEvent args)
    {
        ent.Comp.WarningDamageFlags |= HypertorusDamageFlags.Emped;
    }

    private static float GetIntegrityPercent(HypertorusCoreComponent core)
    {
        var integrity = core.CriticalThresholdProximity / core.MeltingPoint;
        return Math.Max(0, MathF.Round(100 - integrity * 100, 2));
    }

    /// <summary>countdown: 30 секунд обратного отсчёта по общему каналу, затем авария.</summary>
    private void StartCountdown(Entity<HypertorusCoreComponent> ent)
    {
        var core = ent.Comp;
        if (core.FinalCountdown)
            return;

        core.FinalCountdown = true;
        core.CountdownTenths = CountdownTenths;
        core.NextCountdownStep = _timing.CurTime;

        if (IsCritical(core))
        {
            _chat.DispatchGlobalAnnouncement(Loc.GetString("hypertorus-critical-announcement"),
                Loc.GetString("hypertorus-critical-announcement-sender"), colorOverride: Color.Red);
        }

        Radio(ent, Loc.GetString("hypertorus-countdown-start"), core.CommonChannel);
    }

    private void CountdownStep(Entity<HypertorusCoreComponent> ent)
    {
        var core = ent.Comp;
        core.NextCountdownStep = _timing.CurTime + TimeSpan.FromSeconds(1);

        if (core.CriticalThresholdProximity < core.MeltingPoint)
        {
            Radio(ent, Loc.GetString("hypertorus-countdown-aborted"), core.CommonChannel);
            core.FinalCountdown = false;
            return;
        }

        var i = core.CountdownTenths;
        if (i < 0)
        {
            Meltdown(ent);
            return;
        }

        core.CountdownTenths -= 10;
        if (i % 50 != 0 && i > 50)
            return;

        if (i > 50)
        {
            if (i == 100 && IsCritical(core))
                _audio.PlayGlobal(core.CriticalExplosionSound, Filter.Broadcast(), true);

            Radio(ent, Loc.GetString("hypertorus-countdown-seconds", ("seconds", i / 10)), core.CommonChannel);
        }
        else
        {
            Radio(ent, Loc.GetString("hypertorus-countdown-tick", ("seconds", i / 10)), core.CommonChannel);
        }
    }

    private bool IsCritical(HypertorusCoreComponent core)
    {
        return core.SelectedFuel is { } fuel && (_proto.Index(fuel).MeltdownFlags & HypertorusMeltdownFlags.CriticalMeltdown) != 0;
    }

    /// <summary>meltdown: взрыв по флагам рецепта, ЭМИ, радиационный импульс и разлёт газа, ядро исчезает.</summary>
    private void Meltdown(Entity<HypertorusCoreComponent> ent)
    {
        var core = ent.Comp;
        core.FinalCountdown = false;
        var flags = core.SelectedFuel is { } fuelId ? _proto.Index(fuelId).MeltdownFlags : HypertorusMeltdownFlags.BaseExplosion;
        var level = core.PowerLevel;
        var critical = (flags & HypertorusMeltdownFlags.CriticalMeltdown) != 0;
        var emp = (flags & HypertorusMeltdownFlags.Emp) != 0;
        var rad = (flags & HypertorusMeltdownFlags.RadiationPulse) != 0;

        float flash = 0, light = 0, heavy = 0, devastation = 0;
        float empLight = 0, empHeavy = 0, radSize = 0, gasSpread = 0;
        var gasPockets = 0;

        if ((flags & HypertorusMeltdownFlags.BaseExplosion) != 0)
        {
            flash = level * 3;
            light = level * 2;
        }

        if ((flags & HypertorusMeltdownFlags.MediumExplosion) != 0)
        {
            flash = level * 6;
            light = level * 5;
            heavy = level * 0.5f;
        }

        if ((flags & HypertorusMeltdownFlags.DevastatingExplosion) != 0)
        {
            flash = level * 8;
            light = level * 7;
            heavy = level * 2;
            devastation = level;
        }

        if ((flags & HypertorusMeltdownFlags.MinimumSpread) != 0)
        {
            if (emp) { empLight = level * 3; empHeavy = level; }
            if (rad) radSize = 2 * level + 8;
            gasPockets = 5;
            gasSpread = level * 2;
        }

        if ((flags & HypertorusMeltdownFlags.MediumSpread) != 0)
        {
            if (emp) { empLight = level * 5; empHeavy = level * 3; }
            if (rad) radSize = level + 24;
            gasPockets = 7;
            gasSpread = level * 4;
        }

        if ((flags & HypertorusMeltdownFlags.BigSpread) != 0)
        {
            if (emp) { empLight = level * 7; empHeavy = level * 5; }
            if (rad) radSize = level + 34;
            gasPockets = 10;
            gasSpread = level * 6;
        }

        if ((flags & HypertorusMeltdownFlags.MassiveSpread) != 0)
        {
            if (emp) { empLight = level * 9; empHeavy = level * 7; }
            if (rad) radSize = level + 44;
            gasPockets = 15;
            gasSpread = level * 8;
        }

        var coords = _xform.GetMapCoordinates(ent);
        SpreadGas(ent, core.InternalFusion, core.FusionTemp, gasPockets, gasSpread);
        SpreadGas(ent, core.ModeratorInternal, core.ModeratorTemp, gasPockets, gasSpread);

        QueueSs13Explosion(coords,
            critical ? devastation * 2 : devastation,
            critical ? heavy * 2 : heavy,
            light,
            ent);

        if (rad && radSize > 0)
        {
            var pulse = Spawn(core.RadiationPulse, coords);
            _radiation.SetIntensity(pulse, radSize);
        }

        // В SS13 лёгкий радиус ЭМИ без критического сценария берётся из тяжёлого — сохранено.
        if (emp && empHeavy + empLight > 0)
            _emp.EmpPulse(coords, critical ? empLight * 2 : empHeavy, 100000, TimeSpan.FromSeconds(critical ? empHeavy * 4 : empHeavy * 2));

        QueueDel(ent);
    }

    /// <summary>20 % газа разлетается карманами по клеткам в радиусе, остальное — на клетку ядра.</summary>
    private void SpreadGas(EntityUid core, GasMixture mixture, double temperature, int pockets, float spread)
    {
        if (mixture.TotalMoles <= 0)
            return;

        mixture.Temperature = (float) Math.Min(Atmospherics.Tmax, temperature);
        var spreadMix = mixture.RemoveRatio(0.2f);
        if (pockets > 0 && spread > 0 && TryGetTile(core, out var grid, out var center))
        {
            var tiles = new List<Vector2i>();
            var radius = (int) spread;
            for (var x = -radius; x <= radius; x++)
            for (var y = -radius; y <= radius; y++)
            {
                if (x * x + y * y > spread * spread)
                    continue;

                var tile = center + new Vector2i(x, y);
                if (_atmos.GetTileMixture(grid.Owner, null, tile, true) is { Immutable: false })
                    tiles.Add(tile);
            }

            for (var i = 0; i < pockets && tiles.Count > 0; i++)
            {
                var pocket = spreadMix.RemoveRatio(1f / (pockets - i));
                if (_atmos.GetTileMixture(grid.Owner, null, _random.Pick(tiles), true) is { } air)
                    _atmos.Merge(air, pocket);
            }
        }

        if (_atmos.GetContainingMixture(core, false, true) is { } coreAir)
        {
            _atmos.Merge(coreAir, spreadMix);
            _atmos.Merge(coreAir, mixture);
        }
    }

    /// <summary>
    /// explosion(devastation, heavy, light) из SS13 в интенсивность SS14: радиус — лёгкий,
    /// пик растёт с тяжёлым и опустошающим радиусами.
    /// </summary>
    private void QueueSs13Explosion(MapCoordinates coords, float devastation, float heavy, float light, EntityUid cause)
    {
        if (light <= 0 && heavy <= 0 && devastation <= 0)
            return;

        const float slope = 2f;
        var radius = Math.Max(light, Math.Max(heavy, devastation));
        var maxIntensity = 5 + heavy * 5 + devastation * 10;
        _explosion.QueueExplosion(coords, "Default", _explosion.RadiusToIntensity(radius, slope, maxIntensity),
            slope, maxIntensity, cause);
    }

    #endregion
}
