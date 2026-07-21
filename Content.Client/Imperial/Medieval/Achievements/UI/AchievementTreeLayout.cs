using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Imperial.Medieval.Achievements.UI;

public sealed class AchievementTreeLayout : LayoutContainer
{
    private const float HalfCenter = 0.5f;
    private const float EdgeWidth = 1f;
    private const float FeatherPx = 1f;

    private static readonly Color UnlockedEdge = Color.FromHex("#5c8a3ecc");
    private static readonly Color UnlockedCenter = Color.FromHex("#2e5020cc");
    private static readonly Color LockedEdge = Color.FromHex("#5a4428aa");
    private static readonly Color LockedCenter = Color.FromHex("#2a1e0eaa");

    private static readonly Color RootRingUnlocked = Color.FromHex("#d8ac52");
    private static readonly Color RootRingLocked = Color.FromHex("#96702f");

    public readonly record struct RootHalo(Vector2 Center, float HalfSize, bool Unlocked);

    public IReadOnlyDictionary<(string From, string To), List<Vector2>> EdgeCurves { get; set; } =
        new Dictionary<(string From, string To), List<Vector2>>();

    public IReadOnlyList<RootHalo> RootHalos { get; set; } = new List<RootHalo>();

    public Vector2 PanOffset { get; set; }
    public float Zoom { get; set; } = 1f;

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (EdgeCurves.Count > 0)
        {
            var nodesById = new Dictionary<string, AchievementTreeNode>();
            foreach (var child in Children)
            {
                if (child is AchievementTreeNode n)
                    nodesById[n.Proto.ID] = n;
            }

            foreach (var ((fromId, _), basePoints) in EdgeCurves)
            {
                if (basePoints.Count < 2)
                    continue;

                var (edge, center) = nodesById.TryGetValue(fromId, out var parent) && parent.Unlocked
                    ? (UnlockedEdge, UnlockedCenter)
                    : (LockedEdge, LockedCenter);

                DrawCurve(handle, basePoints, edge, center);
            }
        }

        foreach (var halo in RootHalos)
            DrawHalo(handle, halo);
    }

    private Vector2 ToScreen(Vector2 p) => (p * Zoom + PanOffset) * UIScale;

    private void DrawHalo(DrawingHandleScreen handle, RootHalo halo)
    {
        var center = ToScreen(halo.Center);
        var half = halo.HalfSize * Zoom * UIScale;

        if (half < 6f)
            return;

        center = new Vector2(MathF.Round(center.X), MathF.Round(center.Y));

        var ring = halo.Unlocked ? RootRingUnlocked : RootRingLocked;
        var thickness = MathF.Max(1.5f * Zoom, 1f) * UIScale;
        var feather = FeatherPx * UIScale;

        var main = MathF.Round(half + 5f * Zoom * UIScale);
        var echo = MathF.Round(main + 5f * Zoom * UIScale);

        DrawFeatheredSquareRing(handle, center, main, thickness * 2f, 8f * Zoom * UIScale,
            ring.WithAlpha(ring.A * 0.15f));

        DrawFeatheredSquareRing(handle, center, main, thickness, feather, ring);
        DrawFeatheredSquareRing(handle, center, echo, thickness * 0.5f, feather,
            ring.WithAlpha(ring.A * 0.55f));
    }

    private static void DrawFeatheredSquareRing(DrawingHandleScreen handle, Vector2 center, float halfExtent,
        float halfWidth, float feather, Color color)
    {
        var inner = MathF.Max(halfExtent - halfWidth, 0f);
        var outer = halfExtent + halfWidth;
        var innerF = MathF.Max(inner - feather, 0f);
        var outerF = outer + feather;
        var clear = color.WithAlpha(0f);

        var verts = new DrawVertexUV2DColor[3 * 4 * 6];
        var v = 0;

        void Zone(float r1, Color c1, float r2, Color c2)
        {
            void Quad(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color q0, Color q1, Color q2, Color q3)
            {
                verts[v++] = new(p0, q0);
                verts[v++] = new(p1, q1);
                verts[v++] = new(p2, q2);
                verts[v++] = new(p0, q0);
                verts[v++] = new(p2, q2);
                verts[v++] = new(p3, q3);
            }

            // Top
            Quad(center + new Vector2(-r2, -r2), center + new Vector2(r2, -r2),
                center + new Vector2(r1, -r1), center + new Vector2(-r1, -r1),
                c2, c2, c1, c1);
            // Bottom
            Quad(center + new Vector2(-r2, r2), center + new Vector2(r2, r2),
                center + new Vector2(r1, r1), center + new Vector2(-r1, r1),
                c2, c2, c1, c1);
            // Left
            Quad(center + new Vector2(-r2, -r2), center + new Vector2(-r1, -r1),
                center + new Vector2(-r1, r1), center + new Vector2(-r2, r2),
                c2, c1, c1, c2);
            // Right
            Quad(center + new Vector2(r2, -r2), center + new Vector2(r1, -r1),
                center + new Vector2(r1, r1), center + new Vector2(r2, r2),
                c2, c1, c1, c2);
        }

        Zone(innerF, clear, inner, color);
        Zone(inner, color, outer, color);
        Zone(outer, color, outerF, clear);

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, verts);
    }

    private void DrawCurve(DrawingHandleScreen handle, List<Vector2> basePoints, Color edge, Color center)
    {
        var points = new List<Vector2>(basePoints.Count);
        foreach (var basePoint in basePoints)
        {
            var screen = ToScreen(basePoint);
            if (points.Count == 0 || (screen - points[^1]).LengthSquared() > 0.0001f)
                points.Add(screen);
        }

        if (points.Count < 2)
            return;

        var offsets = ComputeJoinOffsets(points);
        var feather = FeatherPx * UIScale;

        DrawBand(handle, points, offsets, (HalfCenter + EdgeWidth) * UIScale, feather, edge);
        DrawBand(handle, points, offsets, HalfCenter * UIScale, feather, center);
    }

    private static Vector2[] ComputeJoinOffsets(List<Vector2> points)
    {
        var count = points.Count;
        var perps = new Vector2[count - 1];

        for (var i = 0; i < count - 1; i++)
        {
            var dir = Vector2.Normalize(points[i + 1] - points[i]);
            perps[i] = new Vector2(-dir.Y, dir.X);
        }

        var offsets = new Vector2[count];
        offsets[0] = perps[0];
        offsets[count - 1] = perps[count - 2];

        for (var i = 1; i < count - 1; i++)
        {
            var sum = perps[i - 1] + perps[i];
            if (sum.LengthSquared() < 0.0001f)
            {
                offsets[i] = perps[i];
                continue;
            }

            var miter = Vector2.Normalize(sum);
            var scale = 1f / MathF.Max(Vector2.Dot(miter, perps[i]), 0.5f);
            offsets[i] = miter * scale;
        }

        return offsets;
    }

    private static void DrawBand(DrawingHandleScreen handle, List<Vector2> points, Vector2[] offsets,
        float halfWidth, float feather, Color color)
    {
        var inner = MathF.Max(halfWidth - feather, 0f);
        var outer = halfWidth + feather;
        var clear = color.WithAlpha(0f);

        var verts = new DrawVertexUV2DColor[(points.Count - 1) * 18];
        var v = 0;

        void Quad(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color c0, Color c1, Color c2, Color c3)
        {
            verts[v++] = new(p0, c0);
            verts[v++] = new(p1, c1);
            verts[v++] = new(p2, c2);
            verts[v++] = new(p0, c0);
            verts[v++] = new(p2, c2);
            verts[v++] = new(p3, c3);
        }

        for (var i = 0; i < points.Count - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            var oa = offsets[i];
            var ob = offsets[i + 1];

            Quad(a - oa * inner, b - ob * inner, b + ob * inner, a + oa * inner, color, color, color, color);
            Quad(a + oa * inner, b + ob * inner, b + ob * outer, a + oa * outer, color, color, clear, clear);
            Quad(a - oa * outer, b - ob * outer, b - ob * inner, a - oa * inner, clear, clear, color, color);
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, verts);
    }
}
