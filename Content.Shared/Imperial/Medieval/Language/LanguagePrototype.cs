using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared.Imperial.Medieval.Language;

[Prototype("language")]
public sealed class LanguagePrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <inheritdoc />
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<LanguagePrototype>))]
    public string[]? Parents { get; }

    /// <inheritdoc />
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; }

    [DataField]
    public int Priority = 1;

    [DataField]
    public bool Roundstart = false;

    [DataField]
    public bool ShowUnderstood = true;

    [DataField]
    public bool Vocal = true;

    [DataField]
    public bool HighIntelligenceAllowed = false;

    [DataField]
    public Color? UiColor;

    public ILanguageType LanguageType
    {
        get
        {
            _languageType.Language = ID;
            return _languageType;
        }
        set => _languageType = value;
    }

    /// <summary>
    /// Тип речи данного языка. Для языков с нестандартной речью, например, коллективного разума или языка жестов
    /// </summary>
    [DataField("speech", required: true, serverOnly: true)]
    private ILanguageType _languageType = null!;

    public ILanguageCondition[] Conditions
    {
        get
        {
            foreach (var item in _conditions)
            {
                item.Language = ID;
            }

            return _conditions;
        }
        set => _conditions = value;
    }

    /// <summary>
    /// Условия, требуемые для отправки / получения (зависит от <see cref="ILanguageCondition.RaiseOnListener"/>) сообщения на данном языке.
    /// </summary>
    [DataField("conditions", serverOnly: true)]
    private ILanguageCondition[] _conditions = Array.Empty<ILanguageCondition>();

    public string LocalizedName => Loc.GetString("language-" + ID + "-name");
    public string LocalizedDescription => Loc.GetString("language-" + ID + "-description");
}
