using System.Globalization;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.UI;

/// <summary>Примитивы для рисования дуг tgui.</summary>
public static class TguiDraw
{
    /// <summary>Толстая дуга; углы в градусах по часовой стрелке от 12 часов.</summary>
    public static void Arc(DrawingHandleScreen handle, Vector2 center, float radius, float width, float fromDeg, float toDeg, Color color)
    {
        var segments = Math.Max(4, (int) MathF.Ceiling(MathF.Abs(toDeg - fromDeg) / 4));
        var verts = new List<Vector2>(segments * 6);
        var inner = radius - width / 2;
        var outer = radius + width / 2;
        for (var i = 0; i < segments; i++)
        {
            var a0 = MathF.PI / 180 * (fromDeg + (toDeg - fromDeg) * i / segments);
            var a1 = MathF.PI / 180 * (fromDeg + (toDeg - fromDeg) * (i + 1) / segments);
            var d0 = new Vector2(MathF.Sin(a0), -MathF.Cos(a0));
            var d1 = new Vector2(MathF.Sin(a1), -MathF.Cos(a1));
            verts.Add(center + d0 * inner);
            verts.Add(center + d0 * outer);
            verts.Add(center + d1 * outer);
            verts.Add(center + d0 * inner);
            verts.Add(center + d1 * outer);
            verts.Add(center + d1 * inner);
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, verts.ToArray(), color);
    }

    public static void Quad(DrawingHandleScreen handle, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, new[] { a, b, c, a, c, d }, color);
    }

    public static void Triangle(DrawingHandleScreen handle, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, new[] { a, b, c }, color);
    }
}
