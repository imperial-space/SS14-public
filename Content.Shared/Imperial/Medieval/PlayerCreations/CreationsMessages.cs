using System.Linq;
using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.PlayerCreations;

[Serializable, NetSerializable]
public sealed class CreationPaintingMessage : IEquatable<CreationPaintingMessage>
{
    public Color[] Painting { get; }
    public string Name { get; }
    public string Description { get; }
    public string Author { get; }
    public NetUserId SenderUserId { get; }
    public string SenderUserName { get; }
    public DateTime CreationTime { get; }

    public CreationPaintingMessage(Color[] painting, string name, string description, string author, NetUserId senderUserId, DateTime creationTime, string senderUserName)
    {
        Painting = painting;
        Name = name;
        Description = description;
        Author = author;
        SenderUserId = senderUserId;
        CreationTime = creationTime;
        SenderUserName = senderUserName;
    }

    public override bool Equals(object? obj) => Equals(obj as CreationPaintingMessage);

    public bool Equals(CreationPaintingMessage? other)
    {
        if (other is null)
            return false;

        return Painting.SequenceEqual(other.Painting);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            foreach (var color in Painting)
                hash = hash * 31 + color.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(CreationPaintingMessage? left, CreationPaintingMessage? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null)
            return false;
        return left.Equals(right);
    }

    public static bool operator !=(CreationPaintingMessage? left, CreationPaintingMessage? right) => !(left == right);

}


[Serializable, NetSerializable]
public sealed class RequestIncomingCreationPaintingsMessage : EuiMessageBase
{
}

[Serializable, NetSerializable]
public sealed class ResponseIncomingCreationPaintingsMessage : EuiMessageBase
{
    public List<CreationPaintingMessage> Paintings;

    public ResponseIncomingCreationPaintingsMessage(List<CreationPaintingMessage> paintings)
    {
        Paintings = paintings;
    }
}

[Serializable, NetSerializable]
public sealed class NewIncomingCreationPaintingMessage : EuiMessageBase
{
    public CreationPaintingMessage Painting;

    public NewIncomingCreationPaintingMessage(CreationPaintingMessage painting)
    {
        Painting = painting;
    }
}

[Serializable, NetSerializable]
public sealed class RemoveIncomingCreationPaintingMessage : EuiMessageBase
{
    public CreationPaintingMessage Painting;

    public RemoveIncomingCreationPaintingMessage(CreationPaintingMessage painting)
    {
        Painting = painting;
    }
}

[Serializable, NetSerializable]
public sealed class AcceptIncomingCreationPaintingMessage : EuiMessageBase
{
    public CreationPaintingMessage Painting;

    public AcceptIncomingCreationPaintingMessage(CreationPaintingMessage painting)
    {
        Painting = painting;
    }
}


[Serializable, NetSerializable]
public sealed class RequestAcceptedCreationPaintingsMessage : EuiMessageBase
{
}

[Serializable, NetSerializable]
public sealed class ResponseAcceptedCreationPaintingsMessage : EuiMessageBase
{
    public List<CreationPaintingMessage> Paintings;

    public ResponseAcceptedCreationPaintingsMessage(List<CreationPaintingMessage> paintings)
    {
        Paintings = paintings;
    }
}

[Serializable, NetSerializable]
public sealed class NewAcceptedCreationPaintingMessage : EuiMessageBase
{
    public CreationPaintingMessage Painting;

    public NewAcceptedCreationPaintingMessage(CreationPaintingMessage painting)
    {
        Painting = painting;
    }
}

[Serializable, NetSerializable]
public sealed class RemoveAcceptedCreationPaintingMessage : EuiMessageBase
{
    public CreationPaintingMessage Painting;

    public RemoveAcceptedCreationPaintingMessage(CreationPaintingMessage painting)
    {
        Painting = painting;
    }
}



[Serializable]
public sealed class SendCreationPaintingEvent : EntityEventArgs
{
    public Color[] Painting;
    public string Name;
    public string Description;
    public string Author;
    public NetUserId SenderPlayer;

    public SendCreationPaintingEvent(Color[] painting, string name, string description, string author, NetUserId senderPlayer)
    {
        Painting = painting;
        Name = name;
        Description = description;
        Author = author;
        SenderPlayer = senderPlayer;
    }
}


[Serializable, NetSerializable]
public sealed class CreationBook : IEquatable<CreationBook>
{
    public string Text { get; }
    public string Name { get; }
    public string Description { get; }
    public string Author { get; }
    public NetUserId SenderUserId { get; }
    public DateTime CreationTime { get; }

    public CreationBook(string text, string name, string description, string author, NetUserId senderUserId, DateTime creationTime)
    {
        Text = text;
        Name = name;
        Description = description;
        Author = author;
        SenderUserId = senderUserId;
        CreationTime = creationTime;
    }

    public override bool Equals(object? obj) => Equals(obj as CreationBook);

    public bool Equals(CreationBook? other)
    {
        if (other is null)
            return false;

        return Text.SequenceEqual(other.Text);
    }

    public override int GetHashCode()
    {
        return Text.GetHashCode();
    }

    public static bool operator ==(CreationBook? left, CreationBook? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null)
            return false;
        return left.Equals(right);
    }

    public static bool operator !=(CreationBook? left, CreationBook? right) => !(left == right);

}


[Serializable, NetSerializable]
public sealed class RequestIncomingCreationBooks : EuiMessageBase
{
}

[Serializable, NetSerializable]
public sealed class ResponseIncomingCreationBooks : EuiMessageBase
{
    public List<CreationBook> Books;

    public ResponseIncomingCreationBooks(List<CreationBook> books)
    {
        Books = books;
    }
}

[Serializable, NetSerializable]
public sealed class NewIncomingCreationBook : EuiMessageBase
{
    public CreationBook Book;

    public NewIncomingCreationBook(CreationBook book)
    {
        Book = book;
    }
}

[Serializable, NetSerializable]
public sealed class RemoveIncomingCreationBook : EuiMessageBase
{
    public CreationBook Book;

    public RemoveIncomingCreationBook(CreationBook book)
    {
        Book = book;
    }
}

[Serializable, NetSerializable]
public sealed class AcceptIncomingCreationBook : EuiMessageBase
{
    public CreationBook Book;

    public AcceptIncomingCreationBook(CreationBook book)
    {
        Book = book;
    }
}


[Serializable, NetSerializable]
public sealed class RequestAcceptedCreationBooks : EuiMessageBase
{
}

[Serializable, NetSerializable]
public sealed class ResponseAcceptedCreationBooks : EuiMessageBase
{
    public List<CreationBook> Books;

    public ResponseAcceptedCreationBooks(List<CreationBook> books)
    {
        Books = books;
    }
}

[Serializable, NetSerializable]
public sealed class NewAcceptedCreationBook : EuiMessageBase
{
    public CreationBook Book;

    public NewAcceptedCreationBook(CreationBook book)
    {
        Book = book;
    }
}

[Serializable, NetSerializable]
public sealed class RemoveAcceptedCreationBook : EuiMessageBase
{
    public CreationBook Book;

    public RemoveAcceptedCreationBook(CreationBook book)
    {
        Book = book;
    }
}

[Serializable, NetSerializable]
public sealed class SendCreationBookEvent : EntityEventArgs
{
    public string Text;
    public string Name;
    public string Description;
    public string Author;
    public NetUserId SenderPlayer;

    public SendCreationBookEvent(string text, string name, string description, string author, NetUserId senderPlayer)
    {
        Text = text;
        Name = name;
        Description = description;
        Author = author;
        SenderPlayer = senderPlayer;
    }
}
