using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Imperial.Lavaland.Zlatozhor;

/// <summary>
/// Zlatozhor — пассивный рудоед. Уходит под землю через <see cref="BurrowDelay"/>.
/// При получении удара отталкивает атакующего.
/// При смерти роняет ценные руды.
/// </summary>
[RegisterComponent]
public sealed partial class ZlatozhorComponent : Component
{
    /// <summary>Задержка перед нырком.</summary>
    [DataField]
    public TimeSpan BurrowDelay = TimeSpan.FromSeconds(10);

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
    public TimeSpan PanicBurrowDelay = TimeSpan.FromSeconds(1.0f);

    /// <summary>Максимальный радиус (в тайлах) телепорта при нырке. 0 = без ограничений.</summary>
    [DataField]
    public float BurrowMaxRange = 20f;
}
