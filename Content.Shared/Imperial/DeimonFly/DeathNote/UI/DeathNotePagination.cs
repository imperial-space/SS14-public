using System.Collections.Immutable;

namespace Content.Shared.Imperial.DeimonFly.DeathNote.UI;

/// <summary>
/// Один физический лист открытого разворота.
/// PageIndex используется только для белых страниц и начинается с нуля.
/// </summary>
public readonly record struct DeathNoteNotebookSheet(
    DeathNoteNotebookSheetKind Kind,
    int PageIndex = -1);

/// <summary>
/// Левый и правый листы, которые одновременно видит пользователь.
/// </summary>
public readonly record struct DeathNoteNotebookSpread(
    DeathNoteNotebookSheet Left,
    DeathNoteNotebookSheet Right);

/// <summary>
/// Чистый расчёт физических разворотов, не зависящий от клиентского интерфейса.
/// </summary>
public static class DeathNotePagination
{
    public static ImmutableArray<DeathNoteNotebookSpread> Build(int writablePageCount)
    {
        writablePageCount = Math.Max(0, writablePageCount);

        // Первый разворот всегда содержит правила слева и первую белую страницу справа.
        var spreadCount = GetSpreadCount(writablePageCount);
        var spreads = ImmutableArray.CreateBuilder<DeathNoteNotebookSpread>(spreadCount);

        for (var spreadIndex = 0; spreadIndex < spreadCount; spreadIndex++)
            spreads.Add(GetSpread(spreadIndex, writablePageCount));

        return spreads.MoveToImmutable();
    }

    public static int GetSpreadCount(int writablePageCount)
    {
        return 1 + Math.Max(0, writablePageCount) / 2;
    }

    public static DeathNoteNotebookSpread GetSpread(int spreadIndex, int writablePageCount)
    {
        writablePageCount = Math.Max(0, writablePageCount);
        spreadIndex = ClampSpreadIndex(spreadIndex, GetSpreadCount(writablePageCount));

        if (spreadIndex == 0)
        {
            return new DeathNoteNotebookSpread(
                new DeathNoteNotebookSheet(DeathNoteNotebookSheetKind.Rules),
                CreateWritableOrBlank(0, writablePageCount));
        }

        return new DeathNoteNotebookSpread(
            CreateWritableOrBlank(spreadIndex * 2 - 1, writablePageCount),
            CreateWritableOrBlank(spreadIndex * 2, writablePageCount));
    }

    public static int GetSpreadIndexForPage(int pageIndex, int writablePageCount)
    {
        if (pageIndex < 0 || pageIndex >= Math.Max(0, writablePageCount))
            return -1;

        return pageIndex == 0 ? 0 : (pageIndex + 1) / 2;
    }

    public static int ClampSpreadIndex(int spreadIndex, int spreadCount)
    {
        return spreadCount <= 0
            ? 0
            : Math.Clamp(spreadIndex, 0, spreadCount - 1);
    }

    public static bool CanGoPrevious(int spreadIndex, int spreadCount)
    {
        return spreadCount > 0 && spreadIndex > 0;
    }

    public static bool CanGoNext(int spreadIndex, int spreadCount)
    {
        return spreadCount > 0 && spreadIndex < spreadCount - 1;
    }

    private static DeathNoteNotebookSheet CreateWritableOrBlank(int pageIndex, int writablePageCount)
    {
        return pageIndex >= 0 && pageIndex < writablePageCount
            ? new DeathNoteNotebookSheet(DeathNoteNotebookSheetKind.Writable, pageIndex)
            : new DeathNoteNotebookSheet(DeathNoteNotebookSheetKind.Blank);
    }
}
