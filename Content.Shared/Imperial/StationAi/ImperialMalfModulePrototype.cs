using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.StationAi;

/// <summary>/datum/ai_module/malf: модуль сбойного ИИ, покупается за процессорное время.</summary>
[Prototype]
public sealed partial class ImperialMalfModulePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public int Cost;

    /// <summary>Destructive Modules / Utility Modules / Upgrade Modules.</summary>
    [DataField(required: true)]
    public ImperialMalfModuleCategory Category;

    /// <summary>one_purchase.</summary>
    [DataField]
    public bool OnePurchase;

    /// <summary>minimum_apcs.</summary>
    [DataField]
    public int MinimumApcs;

    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>power_type: действие, которое выдаёт модуль.</summary>
    [DataField]
    public EntProtoId? Action;

    /// <summary>uses: сколько применений даёт одна покупка (0 — без ограничения).</summary>
    [DataField]
    public int Uses;

    [DataField]
    public LocId? UnlockText;

    [DataField]
    public SoundSpecifier? UnlockSound;

    [DataField]
    public int Order;
}
