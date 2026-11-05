using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Строка ленты событий у всех игроков.</summary>
[Serializable, NetSerializable]
public sealed class CadiaFeedEvent(string text, Color color) : EntityEventArgs
{
    public readonly string Text = text;
    public readonly Color Color = color;
}
