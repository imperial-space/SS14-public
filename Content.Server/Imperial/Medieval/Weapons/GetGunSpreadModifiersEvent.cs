namespace Content.Server.Imperial.Medieval.Weapons;

[ByRefEvent]
public record struct GetGunSpreadModifiersEvent(float Modifier = 0f);
