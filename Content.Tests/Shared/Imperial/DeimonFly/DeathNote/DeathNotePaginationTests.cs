using System.Linq;
using Content.Shared.Imperial.DeimonFly.DeathNote.UI;
using NUnit.Framework;

namespace Content.Tests.Shared.Imperial.DeimonFly.DeathNote;

[TestFixture]
[TestOf(typeof(DeathNotePagination))]
public sealed class DeathNotePaginationTests
{
    [Test]
    public void EmptyNotebookStillHasRulesSpread()
    {
        var spreads = DeathNotePagination.Build(0);

        Assert.Multiple(() =>
        {
            Assert.That(spreads, Has.Length.EqualTo(1));
            Assert.That(spreads[0].Left.Kind, Is.EqualTo(DeathNoteNotebookSheetKind.Rules));
            Assert.That(spreads[0].Right.Kind, Is.EqualTo(DeathNoteNotebookSheetKind.Blank));
        });
    }

    [Test]
    public void FirstWritablePageSharesSpreadWithRules()
    {
        var spread = DeathNotePagination.Build(1).Single();

        Assert.Multiple(() =>
        {
            Assert.That(spread.Left.Kind, Is.EqualTo(DeathNoteNotebookSheetKind.Rules));
            Assert.That(spread.Right.Kind, Is.EqualTo(DeathNoteNotebookSheetKind.Writable));
            Assert.That(spread.Right.PageIndex, Is.Zero);
        });
    }

    [TestCase(2, 2)]
    [TestCase(3, 2)]
    [TestCase(4, 3)]
    [TestCase(30, 16)]
    public void SpreadCountMatchesPhysicalPages(int pageCount, int expectedSpreadCount)
    {
        Assert.Multiple(() =>
        {
            Assert.That(DeathNotePagination.GetSpreadCount(pageCount), Is.EqualTo(expectedSpreadCount));
            Assert.That(DeathNotePagination.Build(pageCount), Has.Length.EqualTo(expectedSpreadCount));
        });
    }

    [Test]
    public void LaterSpreadsHaveWritableLeftAndRightSheets()
    {
        var spreads = DeathNotePagination.Build(3);

        Assert.Multiple(() =>
        {
            Assert.That(spreads[1].Left, Is.EqualTo(
                new DeathNoteNotebookSheet(DeathNoteNotebookSheetKind.Writable, 1)));
            Assert.That(spreads[1].Right, Is.EqualTo(
                new DeathNoteNotebookSheet(DeathNoteNotebookSheetKind.Writable, 2)));
        });
    }

    [Test]
    public void ThirtyWritablePagesAppearExactlyOnce()
    {
        var pageIndices = DeathNotePagination.Build(30)
            .SelectMany(spread => new[] { spread.Left, spread.Right })
            .Where(sheet => sheet.Kind == DeathNoteNotebookSheetKind.Writable)
            .Select(sheet => sheet.PageIndex)
            .ToArray();

        Assert.That(pageIndices, Is.EqualTo(Enumerable.Range(0, 30)));
    }

    [Test]
    public void LastEvenPageCountEndsWithBlankRightSheet()
    {
        var lastSpread = DeathNotePagination.Build(30)[^1];

        Assert.Multiple(() =>
        {
            Assert.That(lastSpread.Left.PageIndex, Is.EqualTo(29));
            Assert.That(lastSpread.Right.Kind, Is.EqualTo(DeathNoteNotebookSheetKind.Blank));
        });
    }

    [TestCase(0, 30, 0)]
    [TestCase(1, 30, 1)]
    [TestCase(2, 30, 1)]
    [TestCase(29, 30, 15)]
    [TestCase(-1, 30, -1)]
    [TestCase(30, 30, -1)]
    public void WritablePageMapsToExpectedSpread(int pageIndex, int pageCount, int expectedSpread)
    {
        Assert.That(DeathNotePagination.GetSpreadIndexForPage(pageIndex, pageCount), Is.EqualTo(expectedSpread));
    }

    [TestCase(-10, 4, 0)]
    [TestCase(2, 4, 2)]
    [TestCase(10, 4, 3)]
    [TestCase(10, 0, 0)]
    public void SpreadIndexIsClamped(int requested, int count, int expected)
    {
        Assert.That(DeathNotePagination.ClampSpreadIndex(requested, count), Is.EqualTo(expected));
    }

    [TestCase(int.MinValue, 30, 0, DeathNoteNotebookSheetKind.Rules, -1, 0)]
    [TestCase(int.MaxValue, 30, 15, DeathNoteNotebookSheetKind.Writable, 29, -1)]
    public void DirectSpreadLookupReturnsClampedAuthoritativeSpread(
        int requestedSpread,
        int pageCount,
        int expectedSpread,
        DeathNoteNotebookSheetKind expectedLeftKind,
        int expectedLeftPage,
        int expectedRightPage)
    {
        var spreadCount = DeathNotePagination.GetSpreadCount(pageCount);
        var actualSpread = DeathNotePagination.ClampSpreadIndex(requestedSpread, spreadCount);
        var spread = DeathNotePagination.GetSpread(requestedSpread, pageCount);

        Assert.Multiple(() =>
        {
            Assert.That(actualSpread, Is.EqualTo(expectedSpread));
            Assert.That(spread.Left.Kind, Is.EqualTo(expectedLeftKind));
            Assert.That(spread.Left.PageIndex, Is.EqualTo(expectedLeftPage));
            Assert.That(spread.Right.PageIndex, Is.EqualTo(expectedRightPage));
        });
    }

    [Test]
    public void NavigationFlagsRespectSpreadEdges()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DeathNotePagination.CanGoPrevious(0, 4), Is.False);
            Assert.That(DeathNotePagination.CanGoPrevious(1, 4), Is.True);
            Assert.That(DeathNotePagination.CanGoNext(2, 4), Is.True);
            Assert.That(DeathNotePagination.CanGoNext(3, 4), Is.False);
        });
    }
}
