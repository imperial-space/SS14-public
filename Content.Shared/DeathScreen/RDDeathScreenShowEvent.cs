/*
 * Project: raincidation
 * File: RDDeathScreenShowEvent.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Robust.Shared.Serialization;

namespace Content.Shared._RD.DeathScreen;

[Serializable, NetSerializable]
public sealed class RDDeathScreenShowEvent : EntityEventArgs
{
    public readonly string Title;
    public readonly string Reason;
    public readonly string AudioPath;

    public RDDeathScreenShowEvent(string title, string reason = "", string audioPath = "")
    {
        Title = title;
        Reason = reason;
        AudioPath = audioPath;
    }

    public override string ToString()
    {
        return $"{Title}: {Reason} ({AudioPath})";
    }
}
