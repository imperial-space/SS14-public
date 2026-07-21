using System.Numerics;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Ships.Islands;

public sealed class IslandRing
{
    public readonly float Inner;
    public readonly float Outer;
    public IslandRing(float inner, float outer) { Inner = inner; Outer = outer; }
}

public readonly struct IslandPlacement
{
    public readonly Vector2 Pos;
    public readonly ResPath Path;
    public readonly float Radius;
    public IslandPlacement(Vector2 pos, ResPath path, float radius) { Pos = pos; Path = path; Radius = radius; }
}

public sealed class IslandSpatialGrid
{
    private readonly float _cell;
    private float _maxR;
    private readonly Dictionary<long, List<IslandPlacement>> _cells = new();

    public IslandSpatialGrid(float cellSize) { _cell = MathF.Max(1f, cellSize); }

    private long Key(int x, int y) => ((long)x << 32) ^ (uint)y;
    private (int, int) CellOf(Vector2 p) =>
        ((int)MathF.Floor(p.X / _cell), (int)MathF.Floor(p.Y / _cell));

    public void Add(IslandPlacement isle)
    {
        _maxR = MathF.Max(_maxR, isle.Radius);
        var (cx, cy) = CellOf(isle.Pos);
        var k = Key(cx, cy);
        if (!_cells.TryGetValue(k, out var list)) { list = new(); _cells[k] = list; }
        list.Add(isle);
    }

    public bool Conflicts(Vector2 p, float radius, float gap)
    {
        var range = (int)MathF.Ceiling((radius + _maxR + gap) / _cell);
        var (cx, cy) = CellOf(p);
        for (var dx = -range; dx <= range; dx++)
        for (var dy = -range; dy <= range; dy++)
            if (_cells.TryGetValue(Key(cx + dx, cy + dy), out var list))
                foreach (var other in list)
                {
                    var min = radius + other.Radius + gap;
                    if (Vector2.DistanceSquared(p, other.Pos) < min * min)
                        return true;
                }
        return false;
    }
}

public sealed class IslandBridsonGenerator
{
    private readonly float _gap;
    private readonly int _maxCandidatesPerPoint;

    public IslandBridsonGenerator(float gap, int maxCandidatesPerPoint = 30) { _gap = gap; _maxCandidatesPerPoint = maxCandidatesPerPoint; }

    public List<IslandPlacement> Generate(
        IslandRing ring,
        List<(ResPath Path, float Radius)> islands,
        IslandSpatialGrid grid,
        Random rng)
    {
        var result = new List<IslandPlacement>();
        if (islands.Count == 0)
            return result;

        var queue = new Queue<(ResPath Path, float Radius)>(Shuffle(islands, rng));

        IslandPlacement? seed = null;
        var (firstPath, firstRadius) = queue.Peek();
        for (var t = 0; t < 64 && seed == null; t++)
        {
            var pos = RandomInRing(ring, rng);
            if (FitsInRing(pos, ring) && !grid.Conflicts(pos, firstRadius, _gap))
                seed = new IslandPlacement(pos, firstPath, firstRadius);
        }
        if (seed == null)
            return result;

        queue.Dequeue();
        var active = new List<IslandPlacement> { seed.Value };
        grid.Add(seed.Value);
        result.Add(seed.Value);

        while (active.Count > 0 && queue.Count > 0)
        {
            var idx = rng.Next(active.Count);
            var origin = active[idx];
            var (nextPath, nextRadius) = queue.Peek();

            var placed = false;
            var dMin = origin.Radius + nextRadius + _gap;

            for (var i = 0; i < _maxCandidatesPerPoint; i++)
            {
                var cand = SampleAnnulus(origin.Pos, dMin, dMin * 2f, rng);
                if (!FitsInRing(cand, ring))
                    continue;
                if (grid.Conflicts(cand, nextRadius, _gap))
                    continue;

                var isle = new IslandPlacement(cand, nextPath, nextRadius);
                queue.Dequeue();
                grid.Add(isle);
                active.Add(isle);
                result.Add(isle);
                placed = true;
                break;
            }

            if (!placed)
                active.RemoveAt(idx);
        }

        return result;
    }

    private static bool FitsInRing(Vector2 p, IslandRing ring)
    {
        var d = p.Length();
        return d >= ring.Inner && d <= ring.Outer;
    }

    private static Vector2 RandomInRing(IslandRing ring, Random rng)
    {
        var u = rng.NextSingle();
        var r = MathF.Sqrt(ring.Inner * ring.Inner + u * (ring.Outer * ring.Outer - ring.Inner * ring.Inner));
        var a = rng.NextSingle() * MathF.Tau;
        return new Vector2(r * MathF.Cos(a), r * MathF.Sin(a));
    }

    private static Vector2 SampleAnnulus(Vector2 center, float inner, float outer, Random rng)
    {
        var u = rng.NextSingle();
        var r = MathF.Sqrt(inner * inner + u * (outer * outer - inner * inner));
        var a = rng.NextSingle() * MathF.Tau;
        return center + new Vector2(r * MathF.Cos(a), r * MathF.Sin(a));
    }

    private static List<T> Shuffle<T>(List<T> source, Random rng)
    {
        var list = new List<T>(source);
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}
