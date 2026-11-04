using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Epsilon;

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
