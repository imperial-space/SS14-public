namespace Content.Shared.Imperial.RandomSteal.Components;

[RegisterComponent]
public sealed partial class RandomStealComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int Chance = 40;
    public EntityUid Item;
    public List<string> ListProto = new List<string> { "MobHuman" }; // Add goblin ofc

}
