using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Imperial.Lavaland.Zlatozhor;

/// <summary>
/// Zlatozhor — пассивный рудоед. Уходит под землю через <see cref="BurrowDelay"/> секунд.
/// При получении удара отталкивает атакующего.
/// При смерти роняет ценные руды.
/// </summary>
[RegisterComponent]
public sealed partial class ZlatozhorComponent : Component
{
    /// <summary>Задержка перед нырком (секунды).</summary>
    [DataField]
    public float BurrowDelay = 10f;

    /// <summary>Момент следующего нырка.</summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan BurrowTime;

    /// <summary>Сила отталкивания атакующего.</summary>
    [DataField]
    public float KnockbackForce = 5f;

    /// <summary>Количество каждой руды, выпадающей при смерти.</summary>
    [DataField]
    public int OreDropAmount = 2;

    /// <summary>Радиус, в котором игрок считается угрозой.</summary>
    [DataField]
    public float DangerVisionRange = 2f;

    /// <summary>Через сколько секунд нырять после обнаружения угрозы.</summary>
    [DataField]
    public float PanicBurrowDelay = 1.0f;
}
