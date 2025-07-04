/*
 * Project: hypercube-mathematics
 * File: FloatingPointEqualsExtension.cs
 * License: MIT
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/technologists-team/hypercube-mathematics
 */

using JetBrains.Annotations;

namespace Content.Shared._RD.Mathematics.Extensions;

[PublicAPI]
public static class FloatingPointEqualsExtension
{
    public static bool AboutEquals(this float a, float b, float tolerance = 1E-15f)
    {
        return HyperMath.AboutEquals(a, b, tolerance);
    }

    public static bool AboutEquals(this double a, double b, double tolerance = 1E-15d)
    {
        return HyperMath.AboutEquals(a, b, tolerance);
    }
}
