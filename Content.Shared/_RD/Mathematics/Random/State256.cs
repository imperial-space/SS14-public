/*
 * Project: hypercube-mathematics
 * File: State256.cs
 * License: MIT
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/technologists-team/hypercube-mathematics
 */

namespace Content.Shared._RD.Mathematics.Random;

[Serializable]
public record struct State256(ulong S0, ulong S1, ulong S2, ulong S3);
