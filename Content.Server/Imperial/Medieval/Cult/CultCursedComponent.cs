using Content.Shared.Damage;
using Content.Shared.Alert;
using Robust.Shared.Prototypes;

namespace Content.Server.Cult.Components;


[RegisterComponent]
public sealed partial class CultCursedComponent : Component
{
    [DataField]
    public ProtoId<AlertPrototype> CurseAlert = "CultCurse";

    [DataField]
    public float CurseLevel = 100f;

    [DataField]
    public float Rate = 0.35f;

    [DataField]
    public float MaxCurseLevel = 100f;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public DamageSpecifier RegenDamage = new()
    {
        DamageDict = new()
            {
                { "Asphyxiation", 1.1 },
                { "Bloodloss", 1.3 },
                { "Blunt", 0.8 },
                { "Heat", 0.5 },
                { "Piercing", 1.1 },
                { "Poison", 0.8 },
                { "Slash", 1.6 },
                { "Shock", 0.8 },
                { "Radiation", 0.8 },
                { "Cold", 0.8 },
                { "Cellular", 0.8 }
            }
    };

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField]
    public DamageSpecifier LostDamage = new()
    {
        DamageDict = new()
            {
                { "Radiation", 1.1 }
            }
    };

}
