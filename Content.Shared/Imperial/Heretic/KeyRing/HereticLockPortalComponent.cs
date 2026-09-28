namespace Content.Shared.Imperial.Heretic.KeyRing;

[RegisterComponent]
public sealed partial class HereticLockPortalComponent : Component
{
    /// <summary>Связанный партнёрский портал.</summary>
    [DataField]
    public EntityUid? Partner;

    /// <summary>Инвертированный режим: если true — язычники идут к партнёру, еретики — рандомно.</summary>
    [DataField]
    public bool Inverted;
}
