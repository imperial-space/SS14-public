using Content.Shared.Alert;
using Content.Shared.Chat.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.EmotePanel;

/// <summary>Нажатие на уведомление «X предлагает…» (/atom/movable/screen/alert/give).</summary>
public sealed partial class ImperialEmoteOfferAlertEvent : BaseAlertEvent;
