using System.Linq;
using Content.Shared.Imperial.Cargo;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cargo;

/// <summary>
/// Биржа материалов TG (SSstock_market и /datum/stock_market_event): цена листа и запас каждого материала
/// раз в минуту сдвигаются по тренду, продажи сбивают цену и пополняют запас.
/// </summary>
public sealed class ImperialStockMarketSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private const int TrendUp = 1;
    private const int TrendStable = 0;
    private const int TrendDown = -1;

    /// <summary>MARKET_EVENT_PROBABILITY, %.</summary>
    private const float EventProbability = 8;

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(60);

    public sealed class MaterialState
    {
        public float Price;
        public int Trend;
        public int TrendLife;
        public int Quantity;
    }

    private enum EventKind { Reset, LargeBoost, LargeDrop, Hotcakes, Lockdown }

    private sealed class MarketEvent
    {
        public EventKind Kind;
        public string Material = string.Empty;
        public int Duration;
    }

    private readonly Dictionary<string, MaterialState> _materials = new();
    private readonly List<MarketEvent> _events = new();
    private TimeSpan _nextFire;

    /// <summary>news_string: сводка событий для новостей.</summary>
    public readonly List<string> News = new();

    public IReadOnlyDictionary<string, MaterialState> Materials => _materials;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnReload);
    }

    private void OnReload(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ImperialStockMaterialPrototype>())
            EnsureInitialized(true);
    }

    /// <summary>SSstock_market.Initialize().</summary>
    private void EnsureInitialized(bool force = false)
    {
        if (_materials.Count > 0 && !force)
            return;

        foreach (var mat in _proto.EnumeratePrototypes<ImperialStockMaterialPrototype>())
        {
            if (_materials.ContainsKey(mat.ID))
                continue;

            var half = mat.BaseQuantity / 2;
            _materials[mat.ID] = new MaterialState
            {
                Price = mat.Value,
                Trend = _random.Next(TrendDown, TrendUp + 1),
                TrendLife = _random.Next(1, 4),
                Quantity = mat.BaseQuantity + _random.Next(-half, half + 1),
            };
        }

        _nextFire = _timing.CurTime + Wait;
    }

    public MaterialState? Get(string material)
    {
        EnsureInitialized();
        return _materials.GetValueOrDefault(material);
    }

    private (float Min, float Max, float Baseline) Limits(ImperialStockMaterialPrototype mat)
    {
        var min = MathF.Round(mat.Minimum ?? mat.Value * 0.5f);
        return (min, MathF.Round(mat.Value * 3), mat.Value);
    }

    /// <summary>adjust_material_price().</summary>
    public void AdjustPrice(string material, float delta)
    {
        if (Get(material) is not { } state || !_proto.TryIndex<ImperialStockMaterialPrototype>(material, out var mat))
            return;

        var (min, max, _) = Limits(mat);
        state.Price = MathF.Round(Math.Clamp(state.Price + delta, min, max));
    }

    /// <summary>adjust_material_quantity().</summary>
    public void AdjustQuantity(string material, int delta)
    {
        if (Get(material) is not { } state || !_proto.TryIndex<ImperialStockMaterialPrototype>(material, out var mat))
            return;

        state.Quantity = Math.Clamp(state.Quantity + delta, 0, mat.BaseQuantity * 2);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        EnsureInitialized();
        if (_timing.CurTime < _nextFire)
            return;

        _nextFire = _timing.CurTime + Wait;

        foreach (var (id, state) in _materials)
        {
            if (_proto.TryIndex<ImperialStockMaterialPrototype>(id, out var mat))
                HandleTrend(mat, state);
        }

        foreach (var ev in _events.ToList())
        {
            HandleEvent(ev);
        }
    }

    private float Gaussian(float mean, float stddev)
    {
        return (float) _random.NextGaussian(mean, stddev);
    }

    /// <summary>handle_trends_and_price().</summary>
    private void HandleTrend(ImperialStockMaterialPrototype mat, MaterialState state)
    {
        if (_random.Prob(EventProbability / 100f))
            StartEvent(mat, state);

        var (min, max, baseline) = Limits(mat);
        var quantityBaseline = mat.BaseQuantity;

        if (state.TrendLife == 0)
        {
            // Чем ближе к краю, тем вероятнее разворот.
            if (state.Price < baseline)
            {
                var chance = 100 - Math.Clamp(state.Price - min, 1, 1000) / (baseline - min) * 100;
                state.Trend = _random.Prob(Math.Clamp(chance, 0, 100) / 100f) ? TrendUp : TrendStable;
            }
            else if (state.Price > baseline)
            {
                var chance = 100 - Math.Clamp(state.Price - max, 1, 1000) / (max - baseline) * 100;
                state.Trend = _random.Prob(Math.Clamp(chance, 0, 100) / 100f) ? TrendDown : TrendStable;
            }

            state.TrendLife = _random.Next(1, 4);
        }
        else
        {
            state.TrendLife -= 1;
        }

        float priceChange = 0;
        float quantityChange = 0;
        switch (state.Trend)
        {
            case TrendUp:
                priceChange = MathF.Ceiling(Gaussian(state.Price * 0.30f, baseline * 0.15f));
                quantityChange = -MathF.Round(Gaussian(quantityBaseline * 0.15f, quantityBaseline * 0.15f));
                break;
            case TrendStable:
                priceChange = MathF.Round(Gaussian(0, baseline * 0.01f));
                quantityChange = MathF.Round(Gaussian(0, quantityBaseline * 0.5f));
                break;
            case TrendDown:
                priceChange = -MathF.Ceiling(Gaussian(state.Price * 0.3f, baseline * 0.15f));
                quantityChange = MathF.Round(Gaussian(quantityBaseline * 0.15f, quantityBaseline * 0.15f));
                break;
        }

        state.Price = MathF.Round(Math.Clamp(state.Price + priceChange, min, max));
        state.Quantity = (int) Math.Clamp(state.Quantity + quantityChange, 0, quantityBaseline * 2);
    }

    #region События

    private void StartEvent(ImperialStockMaterialPrototype mat, MaterialState state)
    {
        var kind = _random.Pick(Enum.GetValues<EventKind>());
        var ev = new MarketEvent { Kind = kind, Material = mat.ID };
        var (min, max, _) = Limits(mat);

        switch (kind)
        {
            case EventKind.Reset:
                state.Trend = TrendStable;
                state.TrendLife = ev.Duration = 1;
                state.Price = mat.Value;
                break;
            case EventKind.LargeBoost:
                state.Trend = TrendUp;
                state.TrendLife = ev.Duration = 4;
                state.Price = Math.Clamp(state.Price + MathF.Round(Gaussian(state.Price, state.Price * 0.15f)), min, max);
                break;
            case EventKind.LargeDrop:
                state.Trend = TrendDown;
                state.TrendLife = ev.Duration = 4;
                state.Price = Math.Clamp(state.Price - MathF.Round(Gaussian(state.Price * 1.5f, state.Price * 0.15f)), min, max);
                break;
            case EventKind.Hotcakes:
                state.Trend = TrendUp;
                state.TrendLife = ev.Duration = 1;
                state.Price = max;
                break;
            case EventKind.Lockdown:
                state.Trend = TrendDown;
                state.TrendLife = ev.Duration = 2;
                break;
        }

        _events.Add(ev);
        if (kind != EventKind.Lockdown)
            AddNews(ev, mat);
    }

    /// <summary>handle() события.</summary>
    private void HandleEvent(MarketEvent ev)
    {
        if (ev.Kind == EventKind.Lockdown && _materials.TryGetValue(ev.Material, out var locked))
            locked.Quantity = 0;

        ev.Duration--;
        if (ev.Duration > 0)
            return;

        _events.Remove(ev);
        if (ev.Kind != EventKind.Lockdown
            || !_materials.TryGetValue(ev.Material, out var state)
            || !_proto.TryIndex<ImperialStockMaterialPrototype>(ev.Material, out var mat))
            return;

        state.Quantity = mat.BaseQuantity;
        state.Price = mat.Value;
        AddNews(ev, mat);
    }

    /// <summary>create_news().</summary>
    private void AddNews(MarketEvent ev, ImperialStockMaterialPrototype mat)
    {
        var key = ev.Kind switch
        {
            EventKind.Reset => "reset",
            EventKind.LargeBoost => "boost",
            EventKind.LargeDrop => "drop",
            EventKind.Hotcakes => "hotcakes",
            _ => "lockdown",
        };

        var company = Loc.GetString($"imperial-stock-company-{_random.Next(1, 9)}");
        var circumstance = Loc.GetString($"imperial-stock-event-{key}-{_random.Next(1, key == "lockdown" ? 3 : 4)}");
        News.Add(Loc.GetString("imperial-stock-news",
            ("event", Loc.GetString($"imperial-stock-event-{key}")),
            ("company", company),
            ("circumstance", circumstance),
            ("material", Loc.GetString(mat.Name))));
    }

    #endregion
}
