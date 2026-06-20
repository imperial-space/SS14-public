namespace Content.Server.Imperial.Lavaland.Megafauna;

[RegisterComponent]
[Access(typeof(MegafaunaVanishOnDeathSystem))]
public sealed partial class MegafaunaVanishOnDeathComponent : Component
{
    [DataField] public float Delay = 0f;
    public bool IsDying;
    public float TimeRemaining;
}
