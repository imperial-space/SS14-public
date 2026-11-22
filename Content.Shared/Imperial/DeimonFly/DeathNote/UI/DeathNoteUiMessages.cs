using System.Collections.Immutable;
using Content.Shared.Imperial.DeimonFly.DeathNote.Models;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.DeimonFly.DeathNote.UI;

/// <summary>
/// Недоверенное клиентское сообщение с новой записью.
/// Автор и тетрадь определяются сервером по BUI-контексту.
/// </summary>
[Serializable, NetSerializable]
public sealed class DeathNoteSubmitMessage : BoundUserInterfaceMessage
{
    public int PageIndex { get; }
    public string EntryText { get; }
    public uint Revision { get; }

    public DeathNoteSubmitMessage(
        int pageIndex,
        string entryText,
        uint revision)
    {
        PageIndex = pageIndex;
        EntryText = entryText;
        Revision = revision;
    }
}

/// <summary>
/// Недоверенный клиентский запрос разворота.
/// Сервер проверяет доступ и возвращает фактический индекс после ограничения диапазона.
/// </summary>
[Serializable, NetSerializable]
public sealed class DeathNoteSpreadRequestMessage : BoundUserInterfaceMessage
{
    public int SpreadIndex { get; }

    public DeathNoteSpreadRequestMessage(int spreadIndex)
    {
        SpreadIndex = spreadIndex;
    }
}

/// <summary>
/// Безопасный адресный ответ автору попытки.
/// Ошибки поиска цели всегда преобразуются сервером в Accepted.
/// </summary>
[Serializable, NetSerializable]
public sealed class DeathNoteSubmissionResponseMessage : BoundUserInterfaceMessage
{
    public DeathNoteSubmissionFeedback Feedback { get; }
    public uint NextRevision { get; }

    public DeathNoteSubmissionResponseMessage(DeathNoteSubmissionFeedback feedback, uint nextRevision)
    {
        Feedback = feedback;
        NextRevision = nextRevision;
    }
}

/// <summary>
/// Адресное безопасное состояние обычной тетради.
/// Оно не хранится в глобальном UserInterfaceComponent и доступно только текущему пользователю BUI.
/// </summary>
[Serializable, NetSerializable]
public sealed class DeathNoteNotebookStateMessage : BoundUserInterfaceMessage
{
    public DeathNoteBoundUserInterfaceState State { get; }

    public DeathNoteNotebookStateMessage(DeathNoteBoundUserInterfaceState state)
    {
        State = state;
    }
}

/// <summary>
/// Недоверенный запрос страницы административного журнала.
/// </summary>
[Serializable, NetSerializable]
public sealed class DeathNoteAdminPageRequestMessage : BoundUserInterfaceMessage
{
    public int Page { get; }

    public DeathNoteAdminPageRequestMessage(int page)
    {
        Page = page;
    }
}

/// <summary>
/// Страница журнала, адресно отправляемая только проверенному администратору.
/// </summary>
[Serializable, NetSerializable]
public sealed class DeathNoteAdminPageResponseMessage : BoundUserInterfaceMessage
{
    public ImmutableArray<DeathNoteAdminEntry> Entries { get; }
    public int Page { get; }
    public int PageCount { get; }
    public int TotalEntries { get; }

    public DeathNoteAdminPageResponseMessage(
        ImmutableArray<DeathNoteAdminEntry> entries,
        int page,
        int pageCount,
        int totalEntries)
    {
        Entries = entries;
        Page = page;
        PageCount = pageCount;
        TotalEntries = totalEntries;
    }
}

/// <summary>
/// Приватное сообщение найденной цели с кастомным ролевым сценарием.
/// </summary>
[Serializable, NetSerializable]
public sealed class DeathNoteInfluenceMessage : EntityEventArgs
{
    public string Scenario { get; }
    public bool IsCustomRoleplay { get; }

    public DeathNoteInfluenceMessage(string scenario, bool isCustomRoleplay)
    {
        Scenario = scenario;
        IsCustomRoleplay = isCustomRoleplay;
    }
}
