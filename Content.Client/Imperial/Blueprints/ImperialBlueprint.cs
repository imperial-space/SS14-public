using System.IO;
using System.Linq;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using YamlDotNet.RepresentationModel;

namespace Content.Client.Imperial.Blueprints;

/// <summary>
/// Чертёж: набор призраков строительства (рецепт, клетка относительно левого нижнего угла, направление).
/// Хранится у игрока в YAML-файле и переживает раунды.
/// </summary>
public sealed class ImperialBlueprint
{
    public string Name = string.Empty;
    public string Author = string.Empty;
    public string Created = string.Empty;
    public List<ImperialBlueprintEntry> Entries = new();

    /// <summary>Имя файла в папке чертежей (без пути).</summary>
    public string File = string.Empty;

    public Vector2i Size => Entries.Count == 0
        ? Vector2i.Zero
        : new Vector2i(Entries.Max(e => e.Offset.X) + 1, Entries.Max(e => e.Offset.Y) + 1);

    public void Write(TextWriter writer)
    {
        var entries = new SequenceDataNode();
        foreach (var entry in Entries)
        {
            var node = new MappingDataNode();
            node.Add("construction", entry.Construction);
            node.Add("x", entry.Offset.X.ToString());
            node.Add("y", entry.Offset.Y.ToString());
            node.Add("dir", entry.Direction.ToString());
            entries.Add(node);
        }

        var root = new MappingDataNode();
        root.Add("name", Name);
        root.Add("author", Author);
        root.Add("created", Created);
        root.Add("entries", entries);

        var stream = new YamlStream(new YamlDocument(root.ToYamlNode()));
        stream.Save(writer, false);
    }

    /// <summary>Прочитать чертёж; null — файл не чертёж или повреждён.</summary>
    public static ImperialBlueprint? Read(TextReader reader)
    {
        var stream = new YamlStream();
        stream.Load(reader);
        if (stream.Documents.Count == 0 || stream.Documents[0].RootNode.ToDataNode() is not MappingDataNode root)
            return null;

        var blueprint = new ImperialBlueprint
        {
            Name = Value(root, "name"),
            Author = Value(root, "author"),
            Created = Value(root, "created"),
        };

        if (!root.TryGet<SequenceDataNode>("entries", out var entries))
            return null;

        foreach (var node in entries.Sequence)
        {
            if (node is not MappingDataNode map
                || !int.TryParse(Value(map, "x"), out var x)
                || !int.TryParse(Value(map, "y"), out var y))
                continue;

            var construction = Value(map, "construction");
            if (construction.Length == 0)
                continue;

            if (!Enum.TryParse<Direction>(Value(map, "dir"), out var dir))
                dir = Direction.South;

            blueprint.Entries.Add(new ImperialBlueprintEntry(construction, new Vector2i(x, y), dir));
        }

        return blueprint;
    }

    private static string Value(MappingDataNode map, string key)
    {
        return map.TryGet<ValueDataNode>(key, out var value) ? value.Value : string.Empty;
    }
}

/// <param name="Construction">ID рецепта строительства (ConstructionPrototype).</param>
/// <param name="Offset">Клетка относительно левого нижнего угла чертежа, в осях сетки.</param>
/// <param name="Direction">Направление постройки относительно сетки.</param>
public readonly record struct ImperialBlueprintEntry(string Construction, Vector2i Offset, Direction Direction);
