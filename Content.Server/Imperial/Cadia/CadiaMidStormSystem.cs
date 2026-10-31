using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Буря посреди поля. Пока держится завеса пилонов, между пилонами Империума и пустошью высадки Хаоса стоит стена бурана:
/// столбец непроходимых клеток через всё поле на каждом этаже (на земле, на стенах и в стоках катакомб). Хаос может
/// высаживаться заранее, но до обороны не дойдёт; с падением завесы буря рассеивается.
/// </summary>
public sealed class CadiaMidStormSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CadiaDefenseRuleSystem _rule = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    private const string WallPrototype = "CadiaMidStormWall";
    private static readonly TimeSpan CheckRate = TimeSpan.FromSeconds(1);

    private TimeSpan _next;
    private readonly List<EntityUid> _walls = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _next)
            return;
        _next = now + CheckRate;
        // Поле пересобрали — старые стены ушли вместе с картами.
        _walls.RemoveAll(wall => TerminatingOrDeleted(wall));

        var up = _rule.TryGetActiveWar(out var war) && CadiaPylonVeilSystem.VeilUp(war);
        if (up && _walls.Count == 0)
            Raise();
        else if (!up && _walls.Count > 0)
            Clear();
    }

    private void Raise()
    {
        var fields = EntityQueryEnumerator<CadiaFieldBoundsComponent, MapGridComponent>();
        while (fields.MoveNext(out var map, out var bounds, out var grid))
        {
            for (var y = (int) MathF.Floor(bounds.Bounds.Bottom); y < (int) MathF.Ceiling(bounds.Bounds.Top); y++)
            {
                // Только на полу: на стенах верхнего этажа и в толще под катакомбами пустота и так непроходима.
                if (_map.GetTileRef(map, grid, new Vector2i(CadiaMidStorm.Column, y)).Tile.IsEmpty)
                    continue;
                _walls.Add(Spawn(WallPrototype, new EntityCoordinates(map, new Vector2(CadiaMidStorm.Column + 0.5f, y + 0.5f))));
            }
        }
    }

    private void Clear()
    {
        foreach (var wall in _walls)
        {
            if (!TerminatingOrDeleted(wall))
                QueueDel(wall);
        }
        _walls.Clear();
    }
}
