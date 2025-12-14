using Content.Shared.Damage;

namespace Content.Shared.Imperial.Medieval
{

    [RegisterComponent]
    public sealed partial class PikeComponent : Component
    {
        [DataField]
        public DamageSpecifier RidingDamage = new()
        {
            DamageDict = new()
            {
                { "Piercing", 14 },
            }
        };
    }
}
