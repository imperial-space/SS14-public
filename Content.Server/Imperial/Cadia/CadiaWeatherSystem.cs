using Content.Shared.Imperial.Cadia;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Погода над полем боя: туман и пепел ограничивают обзор, с падением пилонов чаще идёт пепел, а со штурма стелется гарь.
/// Сервер только выбирает погоду и время, рисует клиент.
/// </summary>
public sealed class CadiaWeatherSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;

    private static readonly TimeSpan MinClear = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan MaxClear = TimeSpan.FromMinutes(6);
    private static readonly TimeSpan MinFront = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan MaxFront = TimeSpan.FromMinutes(4);

    private TimeSpan _next;
    private TimeSpan _nextChange;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _next)
            return;
        _next = now + TimeSpan.FromSeconds(1);

        if (!_rule.TryGetActiveWar(out var war) || war.Phase == CadiaWarPhase.Ended)
        {
            _nextChange = TimeSpan.Zero;
            return;
        }
        if (_nextChange == TimeSpan.Zero)
            _nextChange = now + _random.Next(MinClear, MaxClear);
        if (now < _nextChange)
            return;

        if (war.Weather != CadiaWeather.Clear)
            Set(CadiaWeather.Clear);
        else
            Set(Pick(war));
    }

    /// <summary>Чем больше павших пилонов, тем чаще пепел вместо тумана.</summary>
    private CadiaWeather Pick(CadiaWarStateComponent war)
    {
        if (war.Phase >= CadiaWarPhase.Assault && _random.Prob(0.3f))
            return CadiaWeather.Smog;
        var fallen = war.PylonsTotal > 0 ? 1f - (float) war.PylonsAlive / war.PylonsTotal : 0f;
        return _random.Prob(0.3f + 0.6f * fallen) ? CadiaWeather.Ash : CadiaWeather.Fog;
    }

    public void Set(CadiaWeather weather)
    {
        if (!_rule.TryGetActiveWar(out var war))
            return;
        var now = _timing.CurTime;
        var length = weather == CadiaWeather.Clear ? _random.Next(MinClear, MaxClear) : _random.Next(MinFront, MaxFront);
        war.Weather = weather;
        war.WeatherStart = now;
        war.WeatherEnd = now + length;
        _nextChange = now + length;
        _rule.DirtyWar();
        if (weather != CadiaWeather.Clear)
            _rule.Feed(Loc.GetString($"cadia-feed-weather-{weather.ToString().ToLowerInvariant()}"), CadiaFaction.Chaos);
    }

    /// <summary>Кнопка ведущего: следующая погода по кругу.</summary>
    public void Cycle()
    {
        if (_rule.TryGetActiveWar(out var war))
            Set((CadiaWeather) (((int) war.Weather + 1) % Enum.GetValues<CadiaWeather>().Length));
    }
}
