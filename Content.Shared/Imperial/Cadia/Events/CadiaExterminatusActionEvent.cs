using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Экстерминатус инквизитора: красная кнопка, отдаёт Кадию огню, лишь бы не врагу.</summary>
public sealed partial class CadiaExterminatusActionEvent : InstantActionEvent;
