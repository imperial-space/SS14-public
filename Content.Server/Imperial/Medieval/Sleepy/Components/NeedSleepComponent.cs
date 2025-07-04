using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Content.Shared.Alert;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Set;

namespace Content.Server.NeedSleep.Components;

[RegisterComponent]
public sealed partial class NeedSleepComponent : Component
{
    /// <summary>
    /// Для возможности отключить сонливость
    /// </summary>
    [DataField]
    public bool Enabled = true;

    /// <summary>
    /// Эмоуты, которые будут использоваться при сонливости
    /// </summary>
    [DataField("emotes", customTypeSerializer: typeof(PrototypeIdHashSetSerializer<EmotePrototype>)), ViewVariables(VVAccess.ReadWrite)]
    public HashSet<string> Emotes = new();

    /// <summary>
    /// Грубо говоря, сама сонливость.
    /// </summary>
    [DataField("sleepLevel")]
    private float _sleepLevel = 0f;

    public float SleepLevel
    {
        get => Math.Clamp(_sleepLevel, 0f, MaxSleepLevel);
        set => _sleepLevel = Math.Clamp(value, 0f, MaxSleepLevel);
    }

    /// <summary>
    /// Максимальная сонливость
    /// </summary>
    [DataField]
    public float MaxSleepLevel = 100f;

    /// <summary>
    /// Скорость, с которой сонливость будет накапливаться
    /// </summary>
    [DataField]
    public float SleepLevelPerUpdate = 0.25f;

    /// <summary>
    /// Скорость, с которой сонливость будет восстанавливаться при сне
    /// </summary>
    [DataField]
    public float SleepRegenPerUpdate = 30f;

    [ViewVariables]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    [DataField]
    public float UpdateInterval = 10f;

    public ProtoId<AlertPrototype> TiredAlert = "NeedSleep";
}
