namespace Content.Shared.Imperial.Spellward;

[RegisterComponent]
public sealed partial class SpellwardTestComponent : Component
{
    public EntityUid Object;
    public bool IsFirst = true;
    public EntityUid? Action;
    public readonly string ActionId = "ActionSpellwardSpawn";
}
