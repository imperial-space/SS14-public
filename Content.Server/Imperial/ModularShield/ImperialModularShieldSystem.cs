using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Imperial.MachineParts;
using Content.Server.Power.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.Imperial.MachineParts;
using Content.Shared.Imperial.ModularShield;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Shuttles.Components;
using Content.Shared.Tools.Systems;
using Content.Shared.Wires;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.ModularShield;

/// <summary>
/// Модульные щиты TG (code/game/machinery/modular_shield.dm): генератор и шлюз ставят клетки жёсткого света,
/// урон по клеткам тратит запас прочности генератора; зарядники, ретрансляторы и колодцы через узлы и кабели
/// усиливают генератор; консоль управляет генераторами издалека.
/// </summary>
public sealed partial class ImperialModularShieldSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly Robust.Server.Audio.AudioSystem _audio = default!;

    private const string ShieldProto = "ImperialModularShieldField";
    private static readonly Robust.Shared.Audio.SoundPathSpecifier HitSound = new("/Audio/Imperial/ModularShield/empulse.ogg", Robust.Shared.Audio.AudioParams.Default.WithVolume(-2).WithVariation(0.125f));
    private static readonly ProtoId<Content.Shared.Tools.ToolQualityPrototype> Pulsing = "Pulsing";

    /// <summary>T0C + 200: клетки слабеют в горячем воздухе.</summary>
    private const float HotAtmosTemperature = 473.15f;

    private float _slowAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialModularShieldGeneratorComponent, MapInitEvent>(OnGeneratorMapInit);
        SubscribeLocalEvent<ImperialModularShieldGeneratorComponent, ComponentShutdown>(OnGeneratorShutdown);
        SubscribeLocalEvent<ImperialModularShieldGeneratorComponent, PowerChangedEvent>(OnGeneratorPower);
        SubscribeLocalEvent<ImperialModularShieldGeneratorComponent, ImperialMachinePartsRefreshEvent>(OnGeneratorParts);
        SubscribeLocalEvent<ImperialModularShieldGeneratorComponent, InteractUsingEvent>(OnGeneratorInteractUsing);
        SubscribeLocalEvent<ImperialModularShieldGeneratorComponent, ActivateInWorldEvent>(OnGeneratorActivate);
        SubscribeLocalEvent<ImperialModularShieldGeneratorComponent, ExaminedEvent>(OnGeneratorExamined);
        Subs.BuiEvents<ImperialModularShieldGeneratorComponent>(ImperialModularShieldUiKey.Generator, subs =>
        {
            subs.Event<ImperialModularShieldSetRadiusMessage>(OnSetRadius);
            subs.Event<ImperialModularShieldToggleMessage>((uid, comp, _) => ToggleShields((uid, comp)));
            subs.Event<ImperialModularShieldToggleExteriorMessage>(OnToggleExterior);
        });

        SubscribeLocalEvent<ImperialModularShieldComponent, DamageChangedEvent>(OnShieldDamaged);
        SubscribeLocalEvent<ImperialModularShieldComponent, EmpPulseEvent>(OnShieldEmp);

        InitializeModules();
        InitializeConsole();
    }

    #region Генератор

    private void OnGeneratorMapInit(Entity<ImperialModularShieldGeneratorComponent> ent, ref MapInitEvent args)
    {
        CalculateRegeneration(ent);
        UpdateVisuals(ent);
    }

    private void OnGeneratorShutdown(Entity<ImperialModularShieldGeneratorComponent> ent, ref ComponentShutdown args)
    {
        DeleteShields(ent);
        foreach (var module in ent.Comp.ConnectedModules.ToList())
        {
            if (!TryComp<ImperialModularShieldModuleComponent>(module, out var moduleComp))
                continue;

            moduleComp.ShieldGenerator = null;
            UpdateModuleVisuals((module, moduleComp));
        }
    }

    private void OnGeneratorPower(Entity<ImperialModularShieldGeneratorComponent> ent, ref PowerChangedEvent args)
    {
        if (!args.Powered)
            DeactivateShields(ent);

        UpdateVisuals(ent);
    }

    /// <summary>RefreshParts(): конденсаторы +20 к прочности, сервоприводы +2 к восстановлению, микролазеры +0.25 к радиусу за уровень.</summary>
    private void OnGeneratorParts(Entity<ImperialModularShieldGeneratorComponent> ent, ref ImperialMachinePartsRefreshEvent args)
    {
        var comp = ent.Comp;
        comp.InnateStrength = 40 + 20 * args.Ratings.Sum(ImperialStockPartType.Capacitor, 0);
        comp.InnateRegen = 3 + 2 * args.Ratings.Sum(ImperialStockPartType.Servo, 0);
        comp.InnateRadius = 3 + 0.25f * args.Ratings.Sum(ImperialStockPartType.MicroLaser, 0);

        CalculateRegeneration(ent);
        CalculateMaxStrength(ent);
        CalculateRadius(ent);
    }

    /// <summary>multitool_act(): записать генератор в буфер мультитула.</summary>
    private void OnGeneratorInteractUsing(Entity<ImperialModularShieldGeneratorComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, Pulsing))
            return;

        args.Handled = true;
        EnsureComp<ImperialModularShieldBufferComponent>(args.Used).Generator = ent;
        _popup.PopupEntity(Loc.GetString("imperial-modular-shield-saved-to-buffer"), ent, args.User);
    }

    private void OnGeneratorActivate(Entity<ImperialModularShieldGeneratorComponent> ent, ref ActivateInWorldEvent args)
    {
        // У шлюза нет своего окна.
        if (args.Handled || !args.Complex || ent.Comp.Gate)
            return;

        args.Handled = true;
        _ui.OpenUi(ent.Owner, ImperialModularShieldUiKey.Generator, args.User);
        UpdateGeneratorUi(ent);
    }

    private void OnGeneratorExamined(Entity<ImperialModularShieldGeneratorComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("imperial-modular-shield-examine", ("strength", MathF.Round(ent.Comp.StoredStrength)),
            ("max", MathF.Round(ent.Comp.MaxStrength)), ("active", ent.Comp.Active)));
    }

    /// <summary>toggle_shields().</summary>
    public void ToggleShields(Entity<ImperialModularShieldGeneratorComponent> ent)
    {
        if (ent.Comp.Initiating)
            return;

        if (ent.Comp.Active)
        {
            DeactivateShields(ent);
            UpdateGeneratorUi(ent);
            return;
        }

        if (ent.Comp.Recovering)
            return;

        ActivateShields(ent);
        UpdateGeneratorUi(ent);
    }

    private void OnSetRadius(Entity<ImperialModularShieldGeneratorComponent> ent, ref ImperialModularShieldSetRadiusMessage args)
    {
        if (ent.Comp.Active || !float.IsFinite(args.Radius))
            return;

        var radius = Math.Clamp(args.Radius, 1, ent.Comp.MaxRadius);
        ent.Comp.Radius = radius >= 10 ? MathF.Round(radius) : radius;
        CalculateRegeneration(ent);
        UpdateGeneratorUi(ent);
    }

    private void OnToggleExterior(Entity<ImperialModularShieldGeneratorComponent> ent, ref ImperialModularShieldToggleExteriorMessage args)
    {
        if (ent.Comp.Active)
            return;

        ent.Comp.ExteriorOnly = !ent.Comp.ExteriorOnly;
        CalculateRadius(ent);
        UpdateGeneratorUi(ent);
    }

    /// <summary>deactivate_shields().</summary>
    public void DeactivateShields(Entity<ImperialModularShieldGeneratorComponent> ent)
    {
        ent.Comp.Active = false;
        ent.Comp.Initiating = false;
        ent.Comp.FinishFieldAt = null;
        DeleteShields(ent);
        CalculateRegeneration(ent);
        UpdateVisuals(ent);
    }

    private void DeleteShields(Entity<ImperialModularShieldGeneratorComponent> ent)
    {
        foreach (var shield in ent.Comp.DeployedShields)
        {
            if (!TerminatingOrDeleted(shield))
                QueueDel(shield);
        }

        ent.Comp.DeployedShields.Clear();
    }

    /// <summary>activate_shields(): купол (или стена у шлюза), через 2 секунды становится плотным.</summary>
    public void ActivateShields(Entity<ImperialModularShieldGeneratorComponent> ent)
    {
        var comp = ent.Comp;
        if (comp.Active || !_power.IsPowered(ent.Owner))
            return;

        var xform = Transform(ent);
        if (xform.GridUid is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;

        if (comp.Radius < 0)
            comp.Radius = 3;

        comp.Active = true;
        comp.Initiating = true;
        var center = _map.TileIndicesFor(grid, gridComp, xform.Coordinates);

        if (comp.Gate)
        {
            // Стена вперёд, пока не упрёмся в закрытую клетку или чужой щит.
            var step = xform.LocalRotation.GetCardinalDir().ToIntVec();
            var tile = center;
            comp.Radius = 0;
            for (var i = 0; i < (int) MathF.Round(comp.MaxRadius); i++)
            {
                if (!IsOpen(grid, gridComp, tile) || HasShield(grid, gridComp, tile))
                    break;

                Deploy(ent, grid, gridComp, tile);
                comp.Radius += 1;
                tile += step;
            }

            if (comp.Radius == 0)
            {
                DeactivateShields(ent);
                return;
            }
        }
        else
        {
            var inside = CircleRangeTiles(center, comp.Radius - 1).ToHashSet();
            var ring = comp.Radius >= 10
                ? Perimeter(center, comp.Radius)
                : CircleRangeTiles(center, comp.Radius).Where(t => !inside.Contains(t));

            foreach (var tile in ring.Distinct())
            {
                if (!IsOpen(grid, gridComp, tile) || HasShield(grid, gridComp, tile))
                    continue;

                // Только снаружи: клетки с полом пропускаются.
                if (comp.ExteriorOnly && IsFloor(grid, gridComp, tile))
                    continue;

                Deploy(ent, grid, gridComp, tile);
            }
        }

        comp.FinishFieldAt = _timing.CurTime + TimeSpan.FromSeconds(2);
        CalculateRegeneration(ent);
        UpdateVisuals(ent);
    }

    /// <summary>circle_range_turfs(): dx² + dy² ≤ r × (r + 0.5).</summary>
    private static IEnumerable<Vector2i> CircleRangeTiles(Vector2i center, float radius)
    {
        var rsq = radius * (radius + 0.5f);
        var range = (int) MathF.Floor(radius);
        for (var dx = -range; dx <= range; dx++)
        {
            for (var dy = -range; dy <= range; dy++)
            {
                if (dx * dx + dy * dy <= rsq)
                    yield return center + new Vector2i(dx, dy);
            }
        }
    }

    /// <summary>get_perimeter(): окружность средней точки для больших щитов.</summary>
    private static IEnumerable<Vector2i> Perimeter(Vector2i center, float radius)
    {
        var r = (int) MathF.Floor(radius);
        if (r < 1)
            yield break;

        var t1 = r / 16f;
        var dx = r;
        var dy = 0;
        while (dx >= dy)
        {
            yield return center + new Vector2i(dx, dy);
            yield return center + new Vector2i(-dx, dy);
            yield return center + new Vector2i(dx, -dy);
            yield return center + new Vector2i(-dx, -dy);
            yield return center + new Vector2i(dy, dx);
            yield return center + new Vector2i(-dy, dx);
            yield return center + new Vector2i(dy, -dx);
            yield return center + new Vector2i(-dy, -dx);
            dy += 1;
            t1 += dy;
            var t2 = t1 - dx;
            if (t2 > 0)
            {
                t1 = t2;
                dx -= 1;
            }
        }
    }

    /// <summary>/turf/open: клетка без стены (космос тоже открыт).</summary>
    private bool IsOpen(EntityUid grid, MapGridComponent gridComp, Vector2i tile)
    {
        if (!_map.TryGetTileRef(grid, gridComp, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return true;

        return !_turf.IsTileBlocked(tileRef, CollisionGroup.Impassable);
    }

    private bool IsFloor(EntityUid grid, MapGridComponent gridComp, Vector2i tile)
    {
        return _map.TryGetTileRef(grid, gridComp, tile, out var tileRef) && !tileRef.Tile.IsEmpty && !_turf.IsSpace(tileRef);
    }

    private bool HasShield(EntityUid grid, MapGridComponent gridComp, Vector2i tile)
    {
        var coords = _map.GridTileToLocal(grid, gridComp, tile);
        foreach (var uid in _lookupShields(coords))
            return true;

        return false;
    }

    private IEnumerable<EntityUid> _lookupShields(EntityCoordinates coords)
    {
        var query = EntityManager.System<EntityLookupSystem>().GetEntitiesInRange<ImperialModularShieldComponent>(coords, 0.3f);
        foreach (var shield in query)
            yield return shield.Owner;
    }

    private void Deploy(Entity<ImperialModularShieldGeneratorComponent> ent, EntityUid grid, MapGridComponent gridComp, Vector2i tile)
    {
        var coords = _map.GridTileToLocal(grid, gridComp, tile);
        var shield = Spawn(ShieldProto, coords);
        Comp<ImperialModularShieldComponent>(shield).Generator = ent;

        // На клетках грида щит закреплён, чтобы не пропускать воздух.
        if (_map.TryGetTileRef(grid, gridComp, tile, out var tileRef) && !tileRef.Tile.IsEmpty)
            _transform.AnchorEntity(shield, Transform(shield));

        // Пока поле разворачивается, оно проходимое и полупрозрачное.
        _physics.SetCanCollide(shield, false);
        _appearance.SetData(shield, ImperialModularShieldVisuals.Alpha, 100f / 255f);
        ent.Comp.DeployedShields.Add(shield);
    }

    /// <summary>finish_field().</summary>
    private void FinishField(Entity<ImperialModularShieldGeneratorComponent> ent)
    {
        foreach (var shield in ent.Comp.DeployedShields)
        {
            if (TerminatingOrDeleted(shield))
                continue;

            _physics.SetCanCollide(shield, true);
            _appearance.SetData(shield, ImperialModularShieldVisuals.Alpha, 1f);
        }

        ent.Comp.Initiating = false;
        ent.Comp.FinishFieldAt = null;
        UpdateGeneratorUi(ent);
    }

    /// <summary>calculate_boost(): усилители сети.</summary>
    public void CalculateBoost(Entity<ImperialModularShieldGeneratorComponent> ent)
    {
        var comp = ent.Comp;
        comp.RegenBoost = 0;
        comp.MaxStrengthBoost = 0;
        comp.RadiusBoost = 0;
        foreach (var module in comp.ConnectedModules)
        {
            if (!TryComp<ImperialModularShieldModuleComponent>(module, out var moduleComp))
                continue;

            switch (moduleComp.Kind)
            {
                case ImperialModularShieldModuleKind.Charger:
                    comp.RegenBoost += moduleComp.Boost;
                    break;
                case ImperialModularShieldModuleKind.Well:
                    comp.MaxStrengthBoost += moduleComp.Boost;
                    break;
                case ImperialModularShieldModuleKind.Relay:
                    comp.RadiusBoost += moduleComp.Boost;
                    break;
            }
        }

        CalculateRegeneration(ent);
        CalculateMaxStrength(ent);
        CalculateRadius(ent);
    }

    /// <summary>calculate_radius(): внутренние клетки вдвое уменьшают радиус (кроме шлюза).</summary>
    private void CalculateRadius(Entity<ImperialModularShieldGeneratorComponent> ent)
    {
        var comp = ent.Comp;
        comp.MaxRadius = comp.InnateRadius + comp.RadiusBoost;
        if (!comp.ExteriorOnly && comp.InternalPenalty)
            comp.MaxRadius = MathF.Max(3, comp.MaxRadius * 0.5f);

        if (comp.Radius > comp.MaxRadius)
        {
            DeactivateShields(ent);
            comp.Radius = MathF.Round(comp.MaxRadius);
            return;
        }

        CalculateRegeneration(ent);
    }

    private static void CalculateMaxStrength(Entity<ImperialModularShieldGeneratorComponent> ent)
    {
        ent.Comp.MaxStrength = ent.Comp.InnateStrength + ent.Comp.MaxStrengthBoost;
    }

    /// <summary>calculate_regeneration(): у большого щита восстановление ниже, при внутренних клетках — вдвое.</summary>
    private static void CalculateRegeneration(Entity<ImperialModularShieldGeneratorComponent> ent)
    {
        var comp = ent.Comp;
        comp.MaxRegeneration = comp.InnateRegen + comp.RegenBoost;
        if (!comp.Active)
        {
            comp.CurrentRegeneration = comp.Recovering ? comp.MaxRegeneration * 0.25f : comp.MaxRegeneration;
            return;
        }

        comp.CurrentRegeneration = comp.MaxRegeneration / (0.5f + comp.Radius * 2 / comp.MaxRadius);
        if (!comp.ExteriorOnly && comp.InternalPenalty)
            comp.CurrentRegeneration *= 0.5f;
    }

    /// <summary>shield_drain().</summary>
    public void ShieldDrain(Entity<ImperialModularShieldGeneratorComponent> ent, float amount)
    {
        ent.Comp.StoredStrength -= amount;
        if (ent.Comp.StoredStrength >= 5)
            return;

        ent.Comp.Recovering = true;
        DeactivateShields(ent);
        ent.Comp.StoredStrength = 0;
        UpdateVisuals(ent);
    }

    #endregion

    #region Клетки щита

    /// <summary>Урон по клетке тратит запас генератора; сама клетка неуязвима.</summary>
    private void OnShieldDamaged(Entity<ImperialModularShieldComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        if (ent.Comp.Generator is not { } generator || !TryComp<ImperialModularShieldGeneratorComponent>(generator, out var gen))
        {
            QueueDel(ent);
            return;
        }

        // play_attack_sound(): удар по щиту отзывается импульсом; щит неуязвим, атакующий видит no_damage_feedback.
        _audio.PlayPvs(HitSound, ent);
        if (args.Origin is { } attacker)
            _popup.PopupEntity(Loc.GetString("imperial-modular-shield-hit"), ent, attacker);

        var amount = delta.GetTotal().Float();
        _damageable.ClearAllDamage(ent.Owner);
        ShieldDrain((generator, gen), amount);
    }

    /// <summary>emp_act(): 15 / severity.</summary>
    private void OnShieldEmp(Entity<ImperialModularShieldComponent> ent, ref EmpPulseEvent args)
    {
        if (ent.Comp.Generator is not { } generator || !TryComp<ImperialModularShieldGeneratorComponent>(generator, out var gen))
        {
            QueueDel(ent);
            return;
        }

        args.Affected = true;
        ShieldDrain((generator, gen), 15);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        _slowAccumulator += frameTime;
        var slow = _slowAccumulator >= 1f;
        if (slow)
            _slowAccumulator = 0;

        var query = EntityQueryEnumerator<ImperialModularShieldGeneratorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var ent = (uid, comp);
            if (comp.FinishFieldAt is { } finish && now >= finish)
                FinishField(ent);

            // onShuttleMove(): в прыжке поле падает.
            if (comp.Active && Transform(uid).GridUid is { } grid && HasComp<FTLComponent>(grid))
                DeactivateShields(ent);

            if (!_power.IsPowered(uid))
                continue;

            // process(): восстановление.
            comp.StoredStrength = MathF.Min(comp.StoredStrength + comp.CurrentRegeneration * frameTime, comp.MaxStrength);
            if (comp.StoredStrength >= comp.MaxStrength && comp.Recovering)
            {
                comp.Recovering = false;
                CalculateRegeneration(ent);
                UpdateVisuals(ent);
            }

            if (!slow)
                continue;

            if (comp.Active && comp.DeployedShields.Count > 0)
            {
                // Случайная клетка показывает текущую прочность.
                var shield = _random.Pick(comp.DeployedShields);
                if (!comp.Initiating && !TerminatingOrDeleted(shield))
                    _appearance.SetData(shield, ImperialModularShieldVisuals.Alpha, MathF.Max(comp.StoredStrength / comp.MaxStrength, 40f / 255f));

                HotAtmos(ent);
            }

            UpdateVisuals(ent);
            UpdateGeneratorUi(ent);
        }

        if (slow)
        {
            UpdateModules();
            UpdateConsoles();
        }
    }

    /// <summary>atmos_expose(): горячий воздух (выше 200 °C) у клеток ослабляет щит.</summary>
    private void HotAtmos(Entity<ImperialModularShieldGeneratorComponent> ent)
    {
        foreach (var shield in ent.Comp.DeployedShields.ToList())
        {
            if (TerminatingOrDeleted(shield))
                continue;

            var mixture = _atmos.GetContainingMixture(shield, excite: false);
            if (mixture == null || mixture.Temperature <= HotAtmosTemperature)
                continue;

            ShieldDrain(ent, MathF.Round(mixture.Volume / 400f));
            if (!ent.Comp.Active)
                return;
        }
    }

    /// <summary>update_icon_state(): gen/gate × no_power/recovering/ready × open/closed.</summary>
    private void UpdateVisuals(Entity<ImperialModularShieldGeneratorComponent> ent)
    {
        var open = TryComp<WiresPanelComponent>(ent, out var panel) && panel.Open;
        var power = _power.IsPowered(ent.Owner)
            ? ent.Comp.Recovering ? "recovering_" : "ready_"
            : "no_power_";
        _appearance.SetData(ent, ImperialModularShieldVisuals.State, $"{ent.Comp.IconType}_{power}{(open ? "open" : "closed")}");
    }

    private void UpdateGeneratorUi(Entity<ImperialModularShieldGeneratorComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, ImperialModularShieldUiKey.Generator))
            return;

        var comp = ent.Comp;
        _ui.SetUiState(ent.Owner, ImperialModularShieldUiKey.Generator, new ImperialModularShieldGeneratorState
        {
            MaxRadius = comp.MaxRadius,
            CurrentRadius = comp.Radius,
            MaxStrength = comp.MaxStrength,
            CurrentStrength = comp.StoredStrength,
            MaxRegeneration = comp.MaxRegeneration,
            CurrentRegeneration = comp.CurrentRegeneration,
            Active = comp.Active,
            Recovering = comp.Recovering,
            ExteriorOnly = comp.ExteriorOnly,
            Initiating = comp.Initiating,
        });
    }
}
