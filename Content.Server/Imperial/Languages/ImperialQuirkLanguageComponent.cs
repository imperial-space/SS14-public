using System.Linq;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Radio;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Languages;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Speech;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Languages;

/// <summary>
/// Языковые причуды TG: bilingual (ещё один язык), foreigner (не знает общего), csl (общий понимает частично).
/// Добавляется причудой; применяется один раз при появлении компонента.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialQuirkLanguageComponent : Component
{
    /// <summary>Выдать один случайный язык из списка, которого ещё нет (bilingual).</summary>
    [DataField]
    public List<ProtoId<ImperialLanguagePrototype>> GrantRandom = new();

    [DataField]
    public List<ProtoId<ImperialLanguagePrototype>> Grant = new();

    [DataField]
    public List<ProtoId<ImperialLanguagePrototype>> Block = new();

    /// <summary>Язык перестаёт пониматься полностью, остаётся указанный процент.</summary>
    [DataField]
    public Dictionary<ProtoId<ImperialLanguagePrototype>, int> Partial = new();
}
