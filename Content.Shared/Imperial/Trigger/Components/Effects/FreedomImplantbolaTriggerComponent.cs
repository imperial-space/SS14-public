using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Trigger.Components.Effects;

/// <summary>
/// Помечает сущность как эффект триггера импланта свободы. при срабатывании снимает наручники и освобождает цель от всех опутывающих сущностей
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FreedomImplantbolaTriggerComponent : BaseXOnTriggerComponent;
