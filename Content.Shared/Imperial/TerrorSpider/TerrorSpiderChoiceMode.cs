using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.TerrorSpider;

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
