using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Тело Астартес: ларраманов орган останавливает кровотечение, сус-ан мембрана лечит в критическом состоянии,
/// преомнор защищает от ядов, оссмодула — от переломов, чёрный панцирь нужен для силового доспеха.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CadiaAstartesComponent : Component
{
    /// <summary>Ларраманов орган: сколько кровотечения снимается в секунду.</summary>
    [DataField]
    public float BleedStop = 1f;

    /// <summary>Сус-ан: сколько урона лечится в секунду в критическом состоянии.</summary>
    [DataField]
    public float AnabiosisHeal = 1.5f;

    /// <summary>Прогеноид ещё не извлечён.</summary>
    [DataField]
    public bool HasProgenoid = true;
}
