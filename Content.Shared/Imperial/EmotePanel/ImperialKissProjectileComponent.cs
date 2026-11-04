using Content.Shared.Alert;
using Content.Shared.Chat.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.EmotePanel;

/// <summary>Воздушный поцелуй (/obj/projectile/kiss).</summary>
[RegisterComponent]
public sealed partial class ImperialKissProjectileComponent : Component
{
    /// <summary>Поцелуй вручён лично (suppressed): без сообщения о попадании.</summary>
    [ViewVariables]
    public bool Direct;
}
