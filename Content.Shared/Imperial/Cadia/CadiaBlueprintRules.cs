using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Какой стороне какие чертежи: общая проверка для сервера и кнопок на голокарте.</summary>
public static class CadiaBlueprintRules
{
    public static bool Allowed(CadiaBlueprintKind kind, CadiaFaction faction)
    {
        return kind switch
        {
            CadiaBlueprintKind.ChaosAltar or CadiaBlueprintKind.SpikeBarricade or CadiaBlueprintKind.WarpTrap => faction == CadiaFaction.Chaos,
            CadiaBlueprintKind.Pillbox or CadiaBlueprintKind.Hydra or CadiaBlueprintKind.Wall => faction == CadiaFaction.Imperium,
            _ => true,
        };
    }
}
