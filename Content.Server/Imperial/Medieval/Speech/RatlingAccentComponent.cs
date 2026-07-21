/// <summary>
///     Rattle me bones!
/// </summary>
[RegisterComponent]
public sealed partial class RatlingAccentComponent : Component
{
    /// <summary>
    ///     Chance that the message will be appended with pep-pep"
    /// </summary>
    [DataField("ackChance")]
    public float suffixChance = 0.15f; // Funnier if it doesn't happen every single time
}
