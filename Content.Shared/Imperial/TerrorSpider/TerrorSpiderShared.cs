using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.TerrorSpider;

// Пауки Ужаса 1 в 1 с Paradise (code/modules/mob/living/simple_animal/hostile/terror_spiders).

/// <summary>datum/action/innate/terrorspider/web.</summary>
public sealed partial class TerrorSpiderWebActionEvent : InstantActionEvent;

/// <summary>datum/action/innate/terrorspider/wrap.</summary>
public sealed partial class TerrorSpiderWrapActionEvent : InstantActionEvent;

/// <summary>datum/action/innate/terrorspider/greeneggs.</summary>
public sealed partial class TerrorSpiderGreenEggsActionEvent : InstantActionEvent;

/// <summary>datum/action/innate/terrorspider/ventsmash.</summary>
public sealed partial class TerrorSpiderVentSmashActionEvent : InstantActionEvent;

/// <summary>datum/action/innate/terrorspider/remoteview.</summary>
public sealed partial class TerrorSpiderRemoteViewActionEvent : InstantActionEvent;

/// <summary>datum/action/innate/terrorspider/mother/royaljelly.</summary>
public sealed partial class TerrorSpiderRoyalJellyActionEvent : InstantActionEvent;

/// <summary>datum/action/innate/terrorspider/mother/gatherspiderlings.</summary>
public sealed partial class TerrorSpiderGatherSpiderlingsActionEvent : InstantActionEvent;

/// <summary>datum/action/innate/terrorspider/mother/incubateeggs.</summary>
public sealed partial class TerrorSpiderIncubateEggsActionEvent : InstantActionEvent;

/// <summary>datum/action/innate/terrorspider/queen/queennest.</summary>
public sealed partial class TerrorSpiderQueenNestActionEvent : InstantActionEvent;

/// <summary>datum/action/innate/terrorspider/queen/queensense.</summary>
public sealed partial class TerrorSpiderHiveSenseActionEvent : InstantActionEvent;

/// <summary>datum/action/innate/terrorspider/queen/queeneggs.</summary>
public sealed partial class TerrorSpiderQueenEggsActionEvent : InstantActionEvent;

/// <summary>datum/action/innate/terrorspider/queen/empress/empresslings.</summary>
public sealed partial class TerrorSpiderEmpressLingsActionEvent : InstantActionEvent;

/// <summary>datum/action/innate/terrorspider/queen/empress/empresserase.</summary>
public sealed partial class TerrorSpiderEmpressEraseActionEvent : InstantActionEvent;

/// <summary>datum/spell/princely_charge.</summary>
public sealed partial class TerrorSpiderPrinceChargeActionEvent : WorldTargetActionEvent;

[Serializable, NetSerializable]
public sealed partial class TerrorSpiderWebDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class TerrorSpiderWrapDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class TerrorSpiderVentSmashDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class TerrorSpiderJellyDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class TerrorEggsRemovalDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum TerrorSpiderChoiceUiKey : byte
{
    Key,
}

/// <summary>Что выбирается в окне: tgui_input_list / input() из Paradise.</summary>
[Serializable, NetSerializable]
public enum TerrorSpiderChoiceMode : byte
{
    /// <summary>LayQueenEggs / DoLayGreenEggs: тип яиц и размер кладки.</summary>
    Eggs,

    /// <summary>EmpressLings: число паучат и шанс мертворождения.</summary>
    Spiderlings,

    /// <summary>DoRemoteView: за каким пауком следить.</summary>
    RemoteView,

    /// <summary>FindWrapTarget: что обмотать коконом.</summary>
    Wrap,

    /// <summary>NestPrompt: подтверждение гнездования.</summary>
    Nest,
}

[Serializable, NetSerializable]
public sealed class TerrorSpiderChoiceOption
{
    public string Key { get; }
    public string Label { get; }

    public TerrorSpiderChoiceOption(string key, string label)
    {
        Key = key;
        Label = label;
    }
}

[Serializable, NetSerializable]
public sealed class TerrorSpiderChoiceBoundUserInterfaceState : BoundUserInterfaceState
{
    public TerrorSpiderChoiceMode Mode { get; }
    public string Title { get; }
    public TerrorSpiderChoiceOption[] Options { get; }

    /// <summary>«How many in the batch?»; пусто — без выбора количества.</summary>
    public int[] Counts { get; }

    /// <summary>«%chance to be stillborn?»; пусто — без выбора.</summary>
    public int[] Percents { get; }

    public TerrorSpiderChoiceBoundUserInterfaceState(TerrorSpiderChoiceMode mode, string title,
        TerrorSpiderChoiceOption[] options, int[] counts, int[] percents)
    {
        Mode = mode;
        Title = title;
        Options = options;
        Counts = counts;
        Percents = percents;
    }
}

[Serializable, NetSerializable]
public sealed class TerrorSpiderChoiceMessage : BoundUserInterfaceMessage
{
    public TerrorSpiderChoiceMode Mode { get; }
    public string Key { get; }
    public int Count { get; }
    public int Percent { get; }

    public TerrorSpiderChoiceMessage(TerrorSpiderChoiceMode mode, string key, int count, int percent)
    {
        Mode = mode;
        Key = key;
        Count = count;
        Percent = percent;
    }
}

/// <summary>Смена icon_state: серый паук на паутине (terror_gray_cloaked), окрас принцессы (terror_princess1-3).</summary>
[Serializable, NetSerializable]
public enum TerrorSpiderVisuals : byte
{
    Stage,
}

/// <summary>Спрайты по стадиям: Alive[stage] при жизни, Dead[stage] после смерти.</summary>
[RegisterComponent]
public sealed partial class TerrorSpiderVisualsComponent : Component
{
    [DataField(required: true)]
    public List<string> Alive = new();

    [DataField(required: true)]
    public List<string> Dead = new();
}
