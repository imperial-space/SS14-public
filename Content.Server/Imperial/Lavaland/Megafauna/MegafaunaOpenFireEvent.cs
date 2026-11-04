using Robust.Shared.Audio;

namespace Content.Server.Imperial.Lavaland.Megafauna;

/// <summary>Вызывается раз в тик ИИ, если у босса есть цель и способности не заняты (OpenFire).</summary>
[ByRefEvent]
public record struct MegafaunaOpenFireEvent(EntityUid Target, float Distance);
