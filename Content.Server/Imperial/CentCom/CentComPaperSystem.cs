using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Fax;
using Content.Server.GameTicking.Events;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared.Fax.Components;
using Content.Shared.Imperial.CentCom;
using Content.Shared.Paper;
using Content.Shared.StationRecords;
using Robust.Shared.Random;

namespace Content.Server.Imperial.CentCom;

public sealed class CentComPaperSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly FaxSystem _faxSystem = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly StationRecordsSystem _stationRecordsSystem = default!;

    private static readonly Regex StationCodeRegex = new(@"[A-Z]{2}-\d{3}", RegexOptions.Compiled);

    private static readonly string[] OperatorFirstNames =
    {
        "Алексей", "Дмитрий", "Сергей", "Андрей", "Михаил",
        "Виктор", "Игорь", "Артём", "Николай", "Павел",
    };

    private static readonly string[] OperatorSurnames =
    {
        "Иванов", "Петров", "Соколов", "Михайлов", "Новиков",
        "Фёдоров", "Морозов", "Волков", "Алексеев", "Лебедев",
    };

    private string? _currentOperatorName;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        _currentOperatorName = null;
    }

    public enum SendPaperResult
    {
        Success,
        FaxNotFound,
        MultipleFaxesFound,
    }

    public SendPaperResult SendPaper(CentComPaperPrototype proto, string faxNameQuery, string? operatorName, string? decisionText)
    {
        var matches = new List<(EntityUid Uid, FaxMachineComponent Component)>();
        var enumerator = EntityQueryEnumerator<FaxMachineComponent>();
        while (enumerator.MoveNext(out var uid, out var faxComponent))
        {
            if (faxComponent.FaxName.Contains(faxNameQuery, StringComparison.OrdinalIgnoreCase))
                matches.Add((uid, faxComponent));
        }

        if (matches.Count == 0)
            return SendPaperResult.FaxNotFound;

        if (matches.Count > 1)
            return SendPaperResult.MultipleFaxesFound;

        var (faxUid, faxComp) = matches[0];

        var content = BuildContent(proto, faxUid, operatorName, decisionText);

        var printout = new FaxPrintout(
            content,
            proto.ID,
            null,
            null,
            "paper_stamp-centcom",
            new List<StampDisplayInfo>
            {
                new StampDisplayInfo { StampedName = Loc.GetString("stamp-component-stamped-name-centcom"), StampedColor = Color.FromHex("#BB3232") },
            }
        );
        _faxSystem.Receive(faxUid, printout, null, faxComp);

        return SendPaperResult.Success;
    }

    private string BuildContent(CentComPaperPrototype proto, EntityUid faxUid, string? operatorName, string? decisionText)
    {
        var content = proto.Content;

        var stationCode = GetStationCode(faxUid);
        content = content.Replace("NT14-XX-###", $"NT14-{stationCode}");

        content = content.Replace("3026/ММ/ДД.", $"3026/{GetMoscowDateString()}.");

        content = content.Replace("ПОДОТЧЁТНОЕ ЛИЦО:", $"ПОДОТЧЁТНОЕ ЛИЦО: {GetOrCreateOperatorName(operatorName)}");

        content = content.Replace("Ответственный за выполнение:", $"Ответственный за выполнение: {GetCaptainName(faxUid)}");

        if (proto.RequiresDecision)
            content = content.Replace("(Решение описать тут, эту строку убрать).", decisionText ?? string.Empty);

        return content;
    }

    private string GetMoscowDateString()
    {
        var moscow = DateTime.UtcNow + TimeSpan.FromHours(3);
        return $"{moscow.Month:D2}/{moscow.Day:D2}";
    }

    private string GetStationCode(EntityUid faxUid)
    {
        var station = _stationSystem.GetOwningStation(faxUid);
        var stationName = station != null ? MetaData(station.Value).EntityName : string.Empty;

        var codeMatch = StationCodeRegex.Match(stationName);
        return codeMatch.Success ? codeMatch.Value : "XX-###";
    }

    private string GetOrCreateOperatorName(string? operatorName)
    {
        if (!string.IsNullOrWhiteSpace(operatorName))
        {
            _currentOperatorName = operatorName;
            return operatorName;
        }

        if (_currentOperatorName != null)
            return _currentOperatorName;

        var generated = $"{_random.Pick(OperatorSurnames)} {_random.Pick(OperatorFirstNames)}";
        _currentOperatorName = generated;
        return generated;
    }

    private string GetCaptainName(EntityUid faxUid)
    {
        var station = _stationSystem.GetOwningStation(faxUid);
        if (station == null)
            return Loc.GetString("centcomsendpapercommand-acting-captain");

        var records = _stationRecordsSystem.GetRecordsOfType<GeneralStationRecord>(station.Value);
        var captain = records.FirstOrDefault(r => r.Item2.JobPrototype == "Captain");

        return captain.Item2 != null
            ? captain.Item2.Name
            : Loc.GetString("centcomsendpapercommand-acting-captain");
    }
}
