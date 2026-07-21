using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Content.Client.Imperial.Medieval.Achievements.UI;

internal static class RadialTreeLayout
{
    public sealed class Settings
    {
        public float RingStep = 150f;

        public float NodeSize = 72f;

        public float NodeSeparation = 40f;

        public float StartAngle = -MathF.PI / 2f;
    }

    private const float Tau = MathF.PI * 2f;

    public static LayeredTreeLayout.Result Compute(
        IReadOnlyList<LayeredTreeLayout.Node> nodes,
        IReadOnlyList<LayeredTreeLayout.Edge> edges,
        Settings settings)
    {
        var positions = new Dictionary<string, Vector2>();
        var edgeCurves = new Dictionary<(string From, string To), List<Vector2>>();

        if (nodes.Count == 0)
            return new LayeredTreeLayout.Result(positions, edgeCurves);

        var ids = nodes.Select(n => n.Id).ToHashSet();
        var validEdges = edges.Where(e => ids.Contains(e.FromId) && ids.Contains(e.ToId)).ToList();

        var parent = new Dictionary<string, string>();
        var children = nodes.ToDictionary(n => n.Id, _ => new List<string>());

        foreach (var e in validEdges)
        {
            if (e.FromId == e.ToId || parent.ContainsKey(e.ToId))
                continue;

            if (IsAncestor(e.ToId, e.FromId, parent))
                continue;

            parent[e.ToId] = e.FromId;
            children[e.FromId].Add(e.ToId);
        }

        var roots = nodes.Where(n => !parent.ContainsKey(n.Id)).Select(n => n.Id).ToList();

        var leafCount = new Dictionary<string, int>();

        int CountLeaves(string id)
        {
            if (leafCount.TryGetValue(id, out var cached))
                return cached;

            var ch = children[id];
            var count = ch.Count == 0 ? 1 : ch.Sum(CountLeaves);
            leafCount[id] = count;
            return count;
        }

        foreach (var root in roots)
            CountLeaves(root);

        var angle = new Dictionary<string, float>();
        var depthOf = new Dictionary<string, int>();

        void AssignWedge(string id, float a0, float a1, int depth)
        {
            angle[id] = (a0 + a1) / 2f;
            depthOf[id] = depth;

            var ch = children[id];
            if (ch.Count == 0)
                return;

            var total = ch.Sum(c => leafCount[c]);
            var cursor = a0;

            foreach (var c in ch)
            {
                var span = (a1 - a0) * leafCount[c] / total;
                AssignWedge(c, cursor, cursor + span, depth + 1);
                cursor += span;
            }
        }

        if (roots.Count == 1)
        {
            AssignWedge(roots[0], settings.StartAngle, settings.StartAngle + Tau, 0);
        }
        else
        {
            var total = roots.Sum(r => leafCount[r]);
            var cursor = settings.StartAngle;

            foreach (var r in roots)
            {
                var span = Tau * leafCount[r] / total;
                AssignWedge(r, cursor, cursor + span, 1);
                cursor += span;
            }
        }

        var parentsOf = new Dictionary<string, List<string>>();
        foreach (var e in validEdges)
        {
            if (e.FromId == e.ToId)
                continue;

            if (!parentsOf.TryGetValue(e.ToId, out var list))
                parentsOf[e.ToId] = list = new List<string>();
            list.Add(e.FromId);
        }

        void RotateSubtree(string id, float delta)
        {
            angle[id] += delta;
            foreach (var c in children[id])
                RotateSubtree(c, delta);
        }

        foreach (var n in nodes.OrderBy(n => depthOf[n.Id]))
        {
            if (!parentsOf.TryGetValue(n.Id, out var ps) || ps.Count <= 1)
                continue;

            var sum = Vector2.Zero;
            foreach (var p in ps)
                sum += new Vector2(MathF.Cos(angle[p]), MathF.Sin(angle[p]));

            if (sum.LengthSquared() < 1e-6f)
                continue;

            var delta = MathF.Atan2(sum.Y, sum.X) - angle[n.Id];
            delta = MathF.Atan2(MathF.Sin(delta), MathF.Cos(delta));
            RotateSubtree(n.Id, delta);
        }

        foreach (var key in angle.Keys.ToList())
            angle[key] = ((angle[key] % Tau) + Tau) % Tau;

        var maxDepth = depthOf.Values.Max();
        var radii = new float[maxDepth + 1];
        var minDist = settings.NodeSize + settings.NodeSeparation;

        for (var d = 1; d <= maxDepth; d++)
        {
            var ringAngles = depthOf
                .Where(kv => kv.Value == d)
                .Select(kv => angle[kv.Key])
                .OrderBy(a => a)
                .ToList();

            var needed = 0f;

            if (ringAngles.Count > 1)
            {
                var minGap = Tau;
                for (var i = 0; i < ringAngles.Count; i++)
                {
                    var next = i == ringAngles.Count - 1 ? ringAngles[0] + Tau : ringAngles[i + 1];
                    var gap = next - ringAngles[i];
                    if (gap > 1e-4f)
                        minGap = Math.Min(minGap, gap);
                }

                if (minGap < MathF.PI)
                    needed = minDist / (2f * MathF.Sin(minGap / 2f));
            }

            radii[d] = Math.Max(radii[d - 1] + settings.RingStep, needed);
        }

        var half = settings.NodeSize / 2f;
        var centers = new Dictionary<string, Vector2>();

        foreach (var n in nodes)
        {
            var a = angle[n.Id];
            var center = new Vector2(MathF.Cos(a), MathF.Sin(a)) * radii[depthOf[n.Id]];
            centers[n.Id] = center;
            positions[n.Id] = center - new Vector2(half, half);
        }

        foreach (var e in validEdges)
            edgeCurves[(e.FromId, e.ToId)] = new List<Vector2> { centers[e.FromId], centers[e.ToId] };

        return new LayeredTreeLayout.Result(positions, edgeCurves);
    }

    /// <summary>
    /// Walks the parent chain of <paramref name="descendant"/> checking whether
    /// <paramref name="candidate"/> is already one of its ancestors (cycle guard)
    /// </summary>
    private static bool IsAncestor(string candidate, string descendant, Dictionary<string, string> parent)
    {
        var current = descendant;
        while (parent.TryGetValue(current, out var next))
        {
            if (next == candidate)
                return true;
            current = next;
        }
        return false;
    }
}
