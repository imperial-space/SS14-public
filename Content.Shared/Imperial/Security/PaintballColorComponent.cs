using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

// ─── Пейнтбол (paintball_gun, pepperball) ──────────────────────────────────

/// <summary>Цвет шарика или пятна краски.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class PaintballColorComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color = Color.White;
}
