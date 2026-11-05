using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Bitrunning;

/// <summary>Тревога «Домен завершён» — клик даёт безопасный выход.</summary>
public sealed partial class BitrunningSafeDisconnectAlertEvent : Content.Shared.Alert.BaseAlertEvent;
