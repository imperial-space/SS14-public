using Content.Shared.Imperial.Antimagic;

namespace Content.Server.Imperial.Chaplain;

/// <summary>
/// Очередной тик святой воды в крови. <paramref name="Stuttering"/> — набралось достаточно, чтобы путать речь.
/// </summary>
[ByRefEvent]
public readonly record struct ImperialHolyWaterTickEvent(Entity<ImperialHolyWaterComponent> Holy, TimeSpan Now, bool Stuttering);
