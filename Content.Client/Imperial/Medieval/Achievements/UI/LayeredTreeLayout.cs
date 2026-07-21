using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Content.Client.Imperial.Medieval.Achievements.UI;

/// <summary>
/// Computes a layered graph layout based on the Sugiyama approach using a custom implementation of the Brandes Koepf algorithm.
/// </summary>
internal static class LayeredTreeLayout
{
    /// <summary>
    /// Represents a node in the graph layout.
    /// </summary>
    public readonly struct Node
    {
        public readonly string Id;
        public Node(string id) => Id = id;
    }

    /// <summary>
    /// Represents a directed edge between two nodes.
    /// </summary>
    public readonly struct Edge
    {
        public readonly string FromId;
        public readonly string ToId;
        public Edge(string fromId, string toId) => (FromId, ToId) = (fromId, toId);
    }

    /// <summary>
    /// Configures the configuration settings for the layout engine.
    /// </summary>
    public sealed class Settings
    {
        /// <summary>
        /// Distance between consecutive graph layers.
        /// </summary>
        public float LayerSeparation = 160f;

        /// <summary>
        /// Distance between adjacent nodes within the same layer.
        /// </summary>
        public float NodeSeparation = 60f;

        /// <summary>
        /// Total dimensions of a node bounding box.
        /// </summary>
        public float NodeSize = 72f;

        /// <summary>
        /// Maximum iteration count for layer ordering processes.
        /// </summary>
        public int OrderingIterations = 8;

        /// <summary>
        /// Maximum iteration count for coordinate assignment processes.
        /// </summary>
        public int AlignmentIterations = 8;
    }

    /// <summary>
    /// Contains the calculated coordinates and calculated edge tracks.
    /// </summary>
    public readonly struct Result
    {
        public readonly Dictionary<string, Vector2> Positions;
        public readonly Dictionary<(string From, string To), List<Vector2>> EdgeCurves;

        public Result(
            Dictionary<string, Vector2> positions,
            Dictionary<(string From, string To), List<Vector2>> edgeCurves)
        {
            Positions = positions;
            EdgeCurves = edgeCurves;
        }
    }

    private sealed class InternalNode
    {
        public required string Key;
        public required bool IsDummy;
        public int Layer;
        public int Order;
        public float CrossPos;
    }

    private sealed class InternalEdge
    {
        public required InternalNode From;
        public required InternalNode To;
        public required (string From, string To) OriginalEdge;
    }

    public static Result Compute(
        IReadOnlyList<Node> nodes,
        IReadOnlyList<Edge> edges,
        Settings settings)
    {
        var positions = new Dictionary<string, Vector2>();
        var edgeCurves = new Dictionary<(string From, string To), List<Vector2>>();

        if (nodes.Count == 0)
            return new Result(positions, edgeCurves);

        var ids = nodes.Select(n => n.Id).ToHashSet();
        var validEdges = edges.Where(e => ids.Contains(e.FromId) && ids.Contains(e.ToId)).ToList();

        var offsetY = 0f;
        var componentGap = settings.NodeSize + settings.NodeSeparation * 2f;

        foreach (var (compNodes, compEdges) in SplitComponents(nodes, validEdges))
        {
            var (compPositions, compCurves) = ComputeComponent(compNodes, compEdges, settings);

            var minY = float.MaxValue;
            var maxY = float.MinValue;

            foreach (var pos in compPositions.Values)
            {
                minY = Math.Min(minY, pos.Y);
                maxY = Math.Max(maxY, pos.Y + settings.NodeSize);
            }

            if (minY > maxY)
                continue;

            var shift = new Vector2(0f, offsetY - minY);

            foreach (var (id, pos) in compPositions)
                positions[id] = pos + shift;

            foreach (var (key, points) in compCurves)
                edgeCurves[key] = points.Select(p => p + shift).ToList();

            offsetY += maxY - minY + componentGap;
        }

        return new Result(positions, edgeCurves);
    }

    private static List<(List<Node> Nodes, List<Edge> Edges)> SplitComponents(
        IReadOnlyList<Node> nodes,
        List<Edge> edges)
    {
        var parent = nodes.ToDictionary(n => n.Id, n => n.Id);

        string Find(string x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }

        foreach (var e in edges)
        {
            var a = Find(e.FromId);
            var b = Find(e.ToId);
            if (a != b)
                parent[a] = b;
        }

        var groups = new List<(List<Node> Nodes, List<Edge> Edges)>();
        var indexByRoot = new Dictionary<string, int>();

        foreach (var n in nodes)
        {
            var root = Find(n.Id);
            if (!indexByRoot.TryGetValue(root, out var idx))
            {
                idx = groups.Count;
                indexByRoot[root] = idx;
                groups.Add((new List<Node>(), new List<Edge>()));
            }
            groups[idx].Nodes.Add(n);
        }

        foreach (var e in edges)
            groups[indexByRoot[Find(e.FromId)]].Edges.Add(e);

        return groups;
    }

    private static (Dictionary<string, Vector2> Positions, Dictionary<(string From, string To), List<Vector2>> EdgeCurves) ComputeComponent(
        List<Node> nodes,
        List<Edge> validEdges,
        Settings settings)
    {
        var positions = new Dictionary<string, Vector2>();
        var edgeCurves = new Dictionary<(string From, string To), List<Vector2>>();

        var layer = ComputeLongestPathLayers(nodes, validEdges);

        var (allNodes, chainEdges) = BuildProperGraph(nodes, validEdges, layer);

        var layers = GroupByLayer(allNodes, chainEdges);

        OrderLayers(layers, chainEdges, settings.OrderingIterations);

        AssignCrossCoordinates(layers, chainEdges, settings);

        var half = settings.NodeSize / 2f;

        foreach (var n in allNodes)
        {
            if (n.IsDummy)
                continue;

            var x = n.Layer * settings.LayerSeparation;
            var y = n.CrossPos;
            positions[n.Key] = new Vector2(x, y) - new Vector2(half, half);
        }

        var chainsByOriginalEdge = new Dictionary<(string From, string To), List<InternalEdge>>();
        foreach (var ce in chainEdges)
        {
            if (!chainsByOriginalEdge.TryGetValue(ce.OriginalEdge, out var list))
                chainsByOriginalEdge[ce.OriginalEdge] = list = new List<InternalEdge>();
            list.Add(ce);
        }

        foreach (var edge in validEdges)
        {
            var key = (edge.FromId, edge.ToId);
            var chain = ReconstructChain(key, chainsByOriginalEdge);
            var points = chain
                .Select(n => new Vector2(n.Layer * settings.LayerSeparation, n.CrossPos))
                .ToList();
            edgeCurves[key] = points;
        }

        return (positions, edgeCurves);
    }

    private static Dictionary<string, int> ComputeLongestPathLayers(
        IReadOnlyList<Node> nodes,
        List<Edge> edges)
    {
        var incoming = nodes.ToDictionary(n => n.Id, _ => new List<string>());
        foreach (var e in edges)
            incoming[e.ToId].Add(e.FromId);

        var layer = new Dictionary<string, int>();
        var visiting = new HashSet<string>();

        int Resolve(string id)
        {
            if (layer.TryGetValue(id, out var cached))
                return cached;

            if (!visiting.Add(id))
                return 0;

            var preds = incoming[id];
            var depth = preds.Count == 0 ? 0 : preds.Max(Resolve) + 1;

            visiting.Remove(id);
            layer[id] = depth;
            return depth;
        }

        foreach (var n in nodes)
            Resolve(n.Id);

        return layer;
    }

    private static (List<InternalNode> allNodes, List<InternalEdge> chainEdges) BuildProperGraph(
        IReadOnlyList<Node> nodes,
        List<Edge> edges,
        Dictionary<string, int> layer)
    {
        var allNodes = new List<InternalNode>();
        var byId = new Dictionary<string, InternalNode>();

        foreach (var n in nodes)
        {
            var inode = new InternalNode { Key = n.Id, IsDummy = false, Layer = layer[n.Id] };
            byId[n.Id] = inode;
            allNodes.Add(inode);
        }

        var chainEdges = new List<InternalEdge>();
        var dummyCounter = 0;

        foreach (var e in edges)
        {
            var from = byId[e.FromId];
            var to = byId[e.ToId];
            var original = (e.FromId, e.ToId);

            if (to.Layer - from.Layer <= 1)
            {
                chainEdges.Add(new InternalEdge { From = from, To = to, OriginalEdge = original });
                continue;
            }

            var prev = from;
            for (var l = from.Layer + 1; l < to.Layer; l++)
            {
                var dummyKey = $"__dummy_{dummyCounter++}_{e.FromId}_{e.ToId}_{l}";
                var dummy = new InternalNode { Key = dummyKey, IsDummy = true, Layer = l };
                allNodes.Add(dummy);

                chainEdges.Add(new InternalEdge { From = prev, To = dummy, OriginalEdge = original });
                prev = dummy;
            }

            chainEdges.Add(new InternalEdge { From = prev, To = to, OriginalEdge = original });
        }

        return (allNodes, chainEdges);
    }

    private static List<List<InternalNode>> GroupByLayer(
        List<InternalNode> allNodes,
        List<InternalEdge> chainEdges)
    {
        var maxLayer = allNodes.Count == 0 ? 0 : allNodes.Max(n => n.Layer);

        var layers = new List<List<InternalNode>>();
        for (var i = 0; i <= maxLayer; i++)
            layers.Add(new List<InternalNode>());

        var outgoing = chainEdges
            .GroupBy(e => e.From)
            .ToDictionary(g => g.Key, g => g.Select(e => e.To).ToList());

        var visited = new HashSet<InternalNode>();
        var dfsOrder = new List<InternalNode>();

        void Visit(InternalNode node)
        {
            if (!visited.Add(node))
                return;

            dfsOrder.Add(node);

            if (!outgoing.TryGetValue(node, out var children))
                return;

            children.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

            foreach (var child in children)
                Visit(child);
        }

        var roots = allNodes.Where(n => n.Layer == 0).ToList();
        roots.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

        foreach (var root in roots)
            Visit(root);

        var remaining = allNodes.ToList();
        remaining.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

        foreach (var node in remaining)
            Visit(node);

        foreach (var node in dfsOrder)
            layers[node.Layer].Add(node);

        foreach (var layer in layers)
        {
            for (var i = 0; i < layer.Count; i++)
                layer[i].Order = i;
        }

        return layers;
    }

    private static void OrderLayers(
        List<List<InternalNode>> layers,
        List<InternalEdge> chainEdges,
        int iterations)
    {
        if (layers.Count <= 1)
            return;

        var above = BuildAdjacency(chainEdges, upward: true);
        var below = BuildAdjacency(chainEdges, upward: false);

        for (var iter = 0; iter < iterations; iter++)
        {
            var downPass = iter % 2 == 0;

            if (downPass)
            {
                for (var l = 1; l < layers.Count; l++)
                    BarycenterSort(layers[l], above);
            }
            else
            {
                for (var l = layers.Count - 2; l >= 0; l--)
                    BarycenterSort(layers[l], below);
            }

            foreach (var l in layers)
                Transpose(l, above, below);
        }
    }

    private static Dictionary<InternalNode, List<InternalNode>> BuildAdjacency(
        List<InternalEdge> chainEdges, bool upward)
    {
        var map = new Dictionary<InternalNode, List<InternalNode>>();

        void Add(InternalNode key, InternalNode value)
        {
            if (!map.TryGetValue(key, out var list))
                map[key] = list = new List<InternalNode>();
            list.Add(value);
        }

        foreach (var e in chainEdges)
        {
            if (upward)
                Add(e.To, e.From);
            else
                Add(e.From, e.To);
        }

        return map;
    }

    private static void BarycenterSort(
        List<InternalNode> layer,
        Dictionary<InternalNode, List<InternalNode>> neighbors)
    {
        var scores = new Dictionary<InternalNode, float>();
        foreach (var n in layer)
        {
            if (neighbors.TryGetValue(n, out var adj) && adj.Count > 0)
                scores[n] = (float)adj.Average(a => a.Order);
            else
                scores[n] = n.Order;
        }

        layer.Sort((a, b) => scores[a].CompareTo(scores[b]));
        for (var i = 0; i < layer.Count; i++)
            layer[i].Order = i;
    }

    private static void Transpose(
        List<InternalNode> layer,
        Dictionary<InternalNode, List<InternalNode>> above,
        Dictionary<InternalNode, List<InternalNode>> below)
    {
        var improved = true;
        var guard = 0;

        while (improved && guard++ < 4)
        {
            improved = false;

            for (var i = 0; i < layer.Count - 1; i++)
            {
                var a = layer[i];
                var b = layer[i + 1];

                var before = CountLocalCrossings(a, b, above) + CountLocalCrossings(a, b, below);

                (a.Order, b.Order) = (b.Order, a.Order);
                (layer[i], layer[i + 1]) = (layer[i + 1], layer[i]);

                var after = CountLocalCrossings(layer[i], layer[i + 1], above) +
                            CountLocalCrossings(layer[i], layer[i + 1], below);

                if (after < before)
                {
                    improved = true;
                }
                else
                {
                    (a.Order, b.Order) = (b.Order, a.Order);
                    (layer[i], layer[i + 1]) = (layer[i + 1], layer[i]);
                }
            }
        }
    }

    private static int CountLocalCrossings(
        InternalNode a, InternalNode b,
        Dictionary<InternalNode, List<InternalNode>> neighbors)
    {
        if (!neighbors.TryGetValue(a, out var aAdj)) aAdj = new List<InternalNode>();
        if (!neighbors.TryGetValue(b, out var bAdj)) bAdj = new List<InternalNode>();

        var crossings = 0;
        foreach (var pa in aAdj)
            foreach (var pb in bAdj)
                if (pb.Order < pa.Order)
                    crossings++;

        return crossings;
    }

    private static void AssignCrossCoordinates(
        List<List<InternalNode>> layers,
        List<InternalEdge> chainEdges,
        Settings settings)
    {
        var sep = settings.NodeSeparation + settings.NodeSize;

        foreach (var layer in layers)
            for (var i = 0; i < layer.Count; i++)
                layer[i].CrossPos = i * sep;

        var above = BuildAdjacency(chainEdges, upward: true);
        var below = BuildAdjacency(chainEdges, upward: false);

        for (var iter = 0; iter < settings.AlignmentIterations; iter++)
        {
            var order = iter % 2 == 0
                ? Enumerable.Range(0, layers.Count)
                : Enumerable.Range(0, layers.Count).Reverse();

            foreach (var li in order)
            {
                var layer = layers[li];

                foreach (var n in layer)
                {
                    var target = 0f;
                    var sides = 0;

                    if (above.TryGetValue(n, out var up) && up.Count > 0)
                    {
                        target += Median(up.Select(a => a.CrossPos).OrderBy(v => v).ToList());
                        sides++;
                    }

                    if (below.TryGetValue(n, out var down) && down.Count > 0)
                    {
                        target += Median(down.Select(a => a.CrossPos).OrderBy(v => v).ToList());
                        sides++;
                    }

                    if (sides > 0)
                        n.CrossPos = target / sides;
                }

                CompactLayer(layer, sep);
            }
        }

        var minPos = layers.SelectMany(l => l).DefaultIfEmpty().Min(n => n?.CrossPos ?? 0f);
        foreach (var n in layers.SelectMany(l => l))
            n.CrossPos -= minPos;
    }

    private static float Median(List<float> sorted)
    {
        var n = sorted.Count;
        if (n == 0) return 0f;
        if (n % 2 == 1) return sorted[n / 2];
        return (sorted[n / 2 - 1] + sorted[n / 2]) / 2f;
    }

    private static void CompactLayer(List<InternalNode> layer, float minSep)
    {
        if (layer.Count == 0)
            return;

        var byOrder = layer.OrderBy(n => n.Order).ToList();

        var sums = new List<float>(byOrder.Count);
        var counts = new List<int>(byOrder.Count);

        for (var i = 0; i < byOrder.Count; i++)
        {
            sums.Add(byOrder[i].CrossPos - i * minSep);
            counts.Add(1);

            while (sums.Count > 1 && sums[^2] / counts[^2] > sums[^1] / counts[^1])
            {
                sums[^2] += sums[^1];
                counts[^2] += counts[^1];
                sums.RemoveAt(sums.Count - 1);
                counts.RemoveAt(counts.Count - 1);
            }
        }

        var idx = 0;
        foreach (var (sum, count) in sums.Zip(counts))
        {
            var avg = sum / count;
            for (var k = 0; k < count; k++, idx++)
                byOrder[idx].CrossPos = avg + idx * minSep;
        }
    }

    private static List<InternalNode> ReconstructChain(
        (string From, string To) original,
        Dictionary<(string From, string To), List<InternalEdge>> chainsByOriginalEdge)
    {
        if (!chainsByOriginalEdge.TryGetValue(original, out var segments) || segments.Count == 0)
            return new List<InternalNode>();

        var byFrom = segments.ToDictionary(e => e.From.Key);

        var chain = new List<InternalNode> { segments[0].From };
        var current = segments[0].From;

        while (byFrom.TryGetValue(current.Key, out var edge))
        {
            chain.Add(edge.To);
            current = edge.To;
        }

        return chain;
    }
}
