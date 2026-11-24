namespace Content.Client.Imperial.Blueprints.Types;

/// <summary>
/// Чертёж: набор призраков строительства. Хранится у игрока в YAML-файле (папка Blueprints в данных клиента) и
/// переживает раунды. Чтение и запись — в <see cref="ImperialBlueprintSystem"/>.
/// </summary>
[DataDefinition]
public sealed partial class ImperialBlueprint
{
    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string Author = string.Empty;

    [DataField]
    public string Created = string.Empty;

    [DataField]
    public List<ImperialBlueprintEntry> Entries = new();

    /// <summary>Имя файла в папке чертежей (без пути); в сам файл не пишется.</summary>
    [ViewVariables]
    public string File = string.Empty;
}
