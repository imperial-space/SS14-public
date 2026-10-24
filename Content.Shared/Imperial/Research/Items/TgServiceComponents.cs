using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Портативный экстрактор семян (plants/portaseeder): превращает урожай в сумке в семена.</summary>
[RegisterComponent]
public sealed partial class ImperialPortaseederComponent : Component;

[Serializable, NetSerializable]
public enum ImperialFloraMode : byte
{
    /// <summary>flora/yield: повышает урожайность.</summary>
    Yield,
    /// <summary>flora/mut: вызывает мутации.</summary>
    Mutate,
}

/// <summary>Снаряд флорального соматолуча.</summary>
[RegisterComponent]
public sealed partial class ImperialFloraBeamComponent : Component
{
    [DataField]
    public ImperialFloraMode Mode;
}

/// <summary>Генные ножницы (geneshears): вырезают случайное свойство из растения в лотке.</summary>
[RegisterComponent]
public sealed partial class ImperialGeneShearsComponent : Component
{
    [DataField]
    public float Delay = 5f;
}

[Serializable, NetSerializable]
public sealed partial class ImperialGeneShearsDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Кружка Мауна (maunamug): подогревает содержимое, пока включена.</summary>
[RegisterComponent]
public sealed partial class ImperialHeatingMugComponent : Component
{
    [DataField]
    public string Solution = "drink";

    [DataField]
    public float TargetTemperature = 340f;

    [DataField]
    public float HeatPerSecond = 15f;

    [DataField]
    public bool Enabled;
}

/// <summary>Кресло усиленного допроса (hypnochair): долгий сеанс вводит пристёгнутого в гипнотический транс.</summary>
[RegisterComponent]
public sealed partial class ImperialHypnochairComponent : Component
{
    [DataField]
    public float Duration = 30f;
}

[Serializable, NetSerializable]
public sealed partial class ImperialHypnochairDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Фритюрница (deep_fryer): обжаривает вставленный предмет.</summary>
[RegisterComponent]
public sealed partial class ImperialDeepFryerComponent : Component
{
    [DataField]
    public string Slot = "fryer";

    [DataField]
    public float FryTime = 10f;
}

[Serializable, NetSerializable]
public sealed partial class ImperialDeepFryDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Отмечает обжаренный предмет, чтобы не жарить его повторно.</summary>
[RegisterComponent]
public sealed partial class ImperialDeepFriedComponent : Component;

/// <summary>Мясорубка (gibber): перемалывает мёртвое или умирающее тело в мясо.</summary>
[RegisterComponent]
public sealed partial class ImperialGibberComponent : Component
{
    [DataField]
    public float GibTime = 10f;

    [DataField]
    public EntProtoId DefaultMeat = "FoodMeat";
}

[Serializable, NetSerializable]
public sealed partial class ImperialGibberDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Космическая печь (space_bubble): держит пригодный воздух вокруг себя, пока включена.</summary>
[RegisterComponent]
public sealed partial class ImperialSpaceFurnaceComponent : Component
{
    [DataField]
    public int Radius = 2;

    [DataField]
    public bool Enabled;

    [ViewVariables]
    public TimeSpan NextUpdate;
}

/// <summary>Нейтрализатор аномалий: одноразово гасит аномалию.</summary>
[RegisterComponent]
public sealed partial class ImperialAnomalyNeutralizerComponent : Component;

/// <summary>Пояс инверсии полиморфного поля: сканирует существо и превращает в него носителя.</summary>
[RegisterComponent]
public sealed partial class ImperialPolymorphBeltComponent : Component
{
    [ViewVariables]
    public EntProtoId? Stored;

    [DataField]
    public float Duration = 60f;
}

/// <summary>Шлем перцептоматрицы: защита разума и навязывание галлюцинаций.</summary>
[RegisterComponent]
public sealed partial class ImperialPerceptomatrixComponent : Component
{
    [DataField]
    public EntProtoId Action = "ImperialActionPerceptomatrix";

    [ViewVariables]
    public EntityUid? ActionEntity;

    [DataField]
    public float Duration = 30f;
}

public sealed partial class ImperialPerceptomatrixActionEvent : EntityTargetActionEvent;

/// <summary>Магнитофон (taperecorder): записывает речь вокруг и воспроизводит её.</summary>
[RegisterComponent]
public sealed partial class ImperialTapeRecorderComponent : Component
{
    [DataField]
    public int MaxLines = 30;

    [ViewVariables]
    public bool Recording;

    [ViewVariables]
    public List<string> Lines = new();

    [ViewVariables]
    public int PlaybackIndex = -1;

    [ViewVariables]
    public TimeSpan NextLine;
}

/// <summary>Бритва (razor): сбривает бороду, а без неё — волосы.</summary>
[RegisterComponent]
public sealed partial class ImperialRazorComponent : Component
{
    [DataField]
    public float Delay = 3f;
}

[Serializable, NetSerializable]
public sealed partial class ImperialRazorDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Метла-толкач (pushbroom): в двух руках сметает предметы перед собой.</summary>
[RegisterComponent]
public sealed partial class ImperialPushBroomComponent : Component
{
    [DataField]
    public int MaxItems = 8;
}

/// <summary>Средство для снятия краски.</summary>
[RegisterComponent]
public sealed partial class ImperialPaintRemoverComponent : Component;

/// <summary>E.X.P.E.R.I-MENTOR: опыты над предметами с непредсказуемым результатом.</summary>
[RegisterComponent]
public sealed partial class ImperialExperimentorComponent : Component
{
    [DataField]
    public string Slot = "item";

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public TimeSpan NextUse;
}

[Serializable, NetSerializable]
public enum ImperialExperimentType : byte
{
    Poke,
    Irradiate,
    Gas,
    Heat,
    Cold,
    Obliterate,
}

/// <summary>Кристаллизатор TG: превращает газ из окружающей среды в кристаллы по рецептам.</summary>
[RegisterComponent]
public sealed partial class ImperialCrystallizerComponent : Component
{
    [DataField]
    public List<ImperialCrystallizerRecipe> Recipes = new();

    [ViewVariables]
    public int Selected;

    [ViewVariables]
    public float Progress;

    [DataField]
    public bool Enabled;
}

[DataDefinition]
public sealed partial class ImperialCrystallizerRecipe
{
    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public EntProtoId Result;

    /// <summary>Индекс газа -> моли на один кристалл.</summary>
    [DataField(required: true)]
    public Dictionary<int, float> Gases = new();

    /// <summary>Секунды работы на один кристалл.</summary>
    [DataField]
    public float Time = 30f;
}

[Serializable, NetSerializable]
public enum ImperialFriedVisuals : byte
{
    Fried,
}
