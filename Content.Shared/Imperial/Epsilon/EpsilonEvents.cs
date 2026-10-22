using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Epsilon;

/// <summary>
/// Протокол «Эпсилон»: сервер сообщает клиентам момент старта, клиент сам играет трек
/// и печатает надписи синхронно с ним.
/// </summary>
[Serializable, NetSerializable]
public sealed class EpsilonSequenceStartEvent(TimeSpan start) : EntityEventArgs
{
    public TimeSpan Start = start;
}

[Serializable, NetSerializable]
public sealed class EpsilonSequenceStopEvent : EntityEventArgs;

/// <summary>Общий таймлайн протокола, в секундах от начала трека.</summary>
public static class EpsilonTimeline
{
    /// <summary>CentCom: жёлтый цвет объявлений ЦК.</summary>
    public static readonly Color CentcomYellow = Color.Gold;

    /// <summary>Цвет кода «Эпсилон» (alert_levels.yml: DarkViolet).</summary>
    public static readonly Color EpsilonViolet = Color.DarkViolet;

    public const float LightsOutStart = 7f;
    public const float LightsOutEnd = 10f;
    public const float EmergencyLights = 13.5f;

    /// <summary>1:27.8 — касание пода, смена кода и фиолетовый свет.</summary>
    public const float Landing = 87.8f;
}

/// <summary>
/// Аварийная лампа на время протокола: ровное свечение вместо вращающегося луча мигалки
/// (маска и анимация света — только на клиенте).
/// </summary>
[RegisterComponent, Robust.Shared.GameStates.NetworkedComponent]
public sealed partial class EpsilonSteadyLightComponent : Component
{
    /// <summary>Клиент: маска света до протокола.</summary>
    [ViewVariables]
    public string? OriginalMask;
}
