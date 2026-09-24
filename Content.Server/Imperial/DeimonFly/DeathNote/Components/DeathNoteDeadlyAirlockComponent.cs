using Content.Shared.Damage;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Отмечает шлюз, защитный провод которого отключён Тетрадью смерти.
/// Сохраняется до восстановления штатной защиты шлюза.
/// </summary>
[RegisterComponent]
public sealed partial class DeathNoteDeadlyAirlockComponent : Component
{
    [ViewVariables]
    public uint EntryId;

    [ViewVariables]
    public EntityUid Target;

    [ViewVariables]
    public DamageSpecifier Damage = new();

    [ViewVariables]
    public bool LethalImpactApplied;

    [ViewVariables]
    public TimeSpan? ForceCloseAt;

    [ViewVariables]
    public TimeSpan ForceCloseDelay;

    [ViewVariables]
    public float AutoCloseDelayModifier;

    [ViewVariables]
    public float ImpactRadius;

    [ViewVariables]
    public bool TimingsOverridden;

    [ViewVariables]
    public TimeSpan OriginalCloseTimeOne;

    [ViewVariables]
    public TimeSpan OriginalCloseTimeTwo;

    [ViewVariables]
    public float OriginalAutoCloseDelayModifier;

    [ViewVariables]
    public bool OriginalCanCrush;
}
