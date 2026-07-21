using System.IO;
using System.Linq;
using Robust.Shared.ContentPack;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Ships.Islands;

public static class IslandRadiusParser
{
    private const int ChunkSize = 16;

    public static float? TryComputeRadius(ResPath path, IResourceManager res)
    {
        TextReader? reader = null;
        if (res.UserData.Exists(path))
        {
            reader = res.UserData.OpenText(path);
        }
        else if (res.TryContentFileRead(path, out var stream))
        {
            reader = new StreamReader(stream);
        }

        if (reader == null)
            return null;

        using (reader)
        {
            var documents = DataNodeParser.ParseYamlStream(reader).ToArray();
            if (documents.Length == 0)
                return null;

            if (documents[0].Root is not MappingDataNode root)
                return null;

            if (!root.TryGet<SequenceDataNode>("entities", out var protoGroups))
                return null;

            foreach (var groupNode in protoGroups)
            {
                if (groupNode is not MappingDataNode group)
                    continue;
                if (!group.TryGet<SequenceDataNode>("entities", out var entityList))
                    continue;

                foreach (var entityNode in entityList)
                {
                    if (entityNode is not MappingDataNode entity)
                        continue;
                    if (!entity.TryGet<SequenceDataNode>("components", out var components))
                        continue;

                    foreach (var compNode in components)
                    {
                        if (compNode is not MappingDataNode comp)
                            continue;
                        if (!comp.TryGet<ValueDataNode>("type", out var typeNode) || typeNode.Value != "MapGrid")
                            continue;
                        if (!comp.TryGet<MappingDataNode>("chunks", out var chunks))
                            return null;

                        return ComputeRadius(chunks);
                    }
                }
            }
        }

        return null;
    }

    private static float ComputeRadius(MappingDataNode chunks)
    {
        var minX = int.MaxValue;
        var maxX = int.MinValue;
        var minY = int.MaxValue;
        var maxY = int.MinValue;

        foreach (var key in chunks.Keys)
        {
            var comma = key.IndexOf(',');
            if (comma < 0)
                continue;
            if (!int.TryParse(key.AsSpan(0, comma), out var cx))
                continue;
            if (!int.TryParse(key.AsSpan(comma + 1), out var cy))
                continue;

            if (cx < minX) minX = cx;
            if (cx > maxX) maxX = cx;
            if (cy < minY) minY = cy;
            if (cy > maxY) maxY = cy;
        }

        if (minX == int.MaxValue)
            return 0f;

        var extentX = (maxX - minX + 1) * ChunkSize * 0.5f;
        var extentY = (maxY - minY + 1) * ChunkSize * 0.5f;
        return MathF.Sqrt(extentX * extentX + extentY * extentY);
    }
}
