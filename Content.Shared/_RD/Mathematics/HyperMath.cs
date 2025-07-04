/*
 * Project: hypercube-mathematics
 * File: HyperMath.cs
 * License: MIT
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/technologists-team/hypercube-mathematics
 */

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace Content.Shared._RD.Mathematics;

[PublicAPI]
[SuppressMessage("ReSharper", "RedundantLinebreak")]
[SuppressMessage("ReSharper", "MissingLinebreak")]
public static class HyperMath
{
    /// <summary>
    /// Represents the ratio of the circumference of a circle to its diameter,
    /// specified by the constant, π.
    /// </summary>
    public const double PI = Math.PI;

    /// <summary>
    /// Represents the ratio of the circumference of a circle to its diameter,
    /// specified by the constant, π.
    /// </summary>
    public const float PIf = MathF.PI;

    public const double PIOver2 = PI / 2;
    public const double PIOver4 = PI / 4;
    public const double PIOver6 = PI / 6;
    public const double PITwo = PI * 2;
    public const double ThreePiOver2 = PI / 2 * 3;

    public const double RadiansToDegrees = 180 / PI;
    public const double DegreesToRadians = PI / 180;

    public const float PIOver2F = PIf / 2;
    public const float PIOver4F = PIf / 4;
    public const float PIOver6F = PIf / 6;
    public const float PITwoF = PIf * 2;
    public const float ThreePiOver2F = PIf / 2 * 3;

    public const float RadiansToDegreesF = 180 / PIf;
    public const float DegreesToRadiansF = PIf / 180;

    public static bool AboutEquals(float a, float b, float tolerance = 1E-15f)
    {
        var epsilon = Math.Max(Math.Abs(a), Math.Abs(b)) * tolerance ;
        return Math.Abs(a - b) <= epsilon;
    }

    public static bool AboutEquals(double a, double b, double tolerance = 1E-15d)
    {
        var epsilon = Math.Max(Math.Abs(a), Math.Abs(b)) * tolerance;
        return Math.Abs(a - b) <= epsilon;
    }

    public static byte MoveTowards(byte current, byte target, byte distance)
    {
        return current < target ?
            (byte)Math.Min(current + distance, target) :
            (byte)Math.Max(current - distance, target);
    }

    public static sbyte MoveTowards(sbyte current,sbyte target, sbyte distance)
    {
        return current < target ?
            (sbyte)Math.Min(current + distance, target) :
            (sbyte)Math.Max(current - distance, target);
    }

    public static short MoveTowards(short current, short target, short distance)
    {
        return current < target ?
            (short)Math.Min(current + distance, target) :
            (short)Math.Max(current - distance, target);
    }

    public static ushort MoveTowards(ushort current, ushort target, ushort distance)
    {
        return current < target ?
            (ushort)Math.Min(current + distance, target) :
            (ushort)Math.Max(current - distance, target);
    }

    public static int MoveTowards(int current, int target, int distance)
    {
        return current < target ?
            Math.Min(current + distance, target) :
            Math.Max(current - distance, target);
    }

    public static nint MoveTowards(nint current, nint target, nint distance)
    {
        return current < target ?
            Math.Min(current + distance, target) :
            Math.Max(current - distance, target);
    }

    public static long MoveTowards(long current, long target, long distance)
    {
        return current < target ?
            Math.Min(current + distance, target) :
            Math.Max(current - distance, target);
    }

    public static ulong MoveTowards(ulong current, ulong target, ulong distance)
    {
        return current < target ?
            Math.Min(current + distance, target) :
            Math.Max(current - distance, target);
    }

    public static uint MoveTowards(uint current, uint target, uint distance)
    {
        return current < target ?
            Math.Min(current + distance, target) :
            Math.Max(current - distance, target);
    }

    public static float MoveTowards(float current, float target, float distance)
    {
        return current < target ?
            MathF.Min(current + distance, target) :
            MathF.Max(current - distance, target);
    }

    public static double MoveTowards(double current, double target, double distance)
    {
        return current < target ?
            Math.Min(current + distance, target) :
            Math.Max(current - distance, target);
    }
}
