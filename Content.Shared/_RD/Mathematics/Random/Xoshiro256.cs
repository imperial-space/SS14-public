/*
 * Project: hypercube-mathematics
 * File: Xoshiro256.cs
 * License: MIT
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/technologists-team/hypercube-mathematics
 */

using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace Content.Shared._RD.Mathematics.Random;

/// <remarks>
/// Period 2 ^ 256 - 1.
/// Link to a wikipedia page with an explanation of how it works and examples of implementation: https://en.wikipedia.org/wiki/Xorshift
/// </remarks>
[PublicAPI, Serializable]
public struct Xoshiro256
{
    private ulong _s0, _s1, _s2, _s3;

    /// <summary>
    /// Gets the current seed value that could be used to recreate this generator's state.
    /// Note: This is not the original seed but a derived value representing current state.
    /// </summary>
    public State256 State
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_s0, _s1, _s2, _s3);
    }

    /// <summary>
    /// Gets the current seed value that could be used to recreate this generator's state.
    /// Note: This is not the original seed but a derived value representing current state.
    /// </summary>
    public long Seed
    {
        // Combine state values to create a single seed value
        // Ensure positive value
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (long) ((_s0 ^ _s1 ^ _s2 ^ _s3) & 0x7FFFFFFFFFFFFFFF);
    }

    /// <summary>
    /// Initialization of the generator from a 32-bit seed.
    /// </summary>
    public Xoshiro256(long seed)
    {
        // Use SplitMix64 to initialize states
        _s0 = SplitMix64((ulong) seed);
        _s1 = SplitMix64(_s0);
        _s2 = SplitMix64(_s1);
        _s3 = SplitMix64(_s2);

        // Additional mixing to eliminate correlations
        for (var i = 0; i < 8; i++)
        {
            NextULong();
        }
    }

    /// <summary>
    /// Initialization of the generator from a 256-bit state.
    /// </summary>
    public Xoshiro256(State256 state)
    {
        // Use SplitMix64 to initialize states
        _s0 = state.S0;
        _s1 = state.S1;
        _s2 = state.S2;
        _s3 = state.S3;
    }

    /// <summary>
    /// Generation of float in the range [0, 1].
    /// </summary>
    public float NextFloat()
    {
        // We use 23 bits of the mantissa (IEEE 754 standard)
        return (NextULong() >> 40) * (1.0f / ((1 << 24) - 1));
    }

    public float NextFloat(float min, float max)
    {
        if (max < min)
            return min;

        var range = max - min;
        return min + NextFloat() * range;
    }

    /// <summary>
    /// Generation of int in the range [min, max].
    /// </summary>
    public int NextInt(int min, int max)
    {
        if (max <= min)
            return min;

        var range = (ulong) (max - min);
        return min + (int) (NextULong() % range);
    }

    /// <summary>
    /// Generation of int in the range [0, max].
    /// </summary>
    public int NextInt(int max)
    {
        return NextInt(0, max);
    }

    public int NextInt()
    {
        return (int) NextULong();
    }

    /// <summary>
    /// Main generation function (xoshiro256** algorithm).
    /// </summary>
    private ulong NextULong()
    {
        var result = Rotl(_s1 * 5, 7) * 9;
        var t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = Rotl(_s3, 45);

        return result;
    }

    /// <summary>
    /// Auxiliary function - cyclic shift to the left.
    /// </summary>
    private static ulong Rotl(ulong x, int k)
    {
        return (x << k) | (x >> (64 - k));
    }

    /// <summary>
    /// SplitMix64 for state initialization.
    /// </summary>
    private static ulong SplitMix64(ulong x)
    {
        x += 0x9E3779B97F4A7C15;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EB;
        return x ^ (x >> 31);
    }
}
