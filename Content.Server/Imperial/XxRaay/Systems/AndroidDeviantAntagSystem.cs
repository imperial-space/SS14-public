using Content.Server.Imperial.XxRaay.Android;
using Content.Shared.Imperial.XxRaay.Android;
using Content.Shared.Objectives.Components;
using Content.Shared.Objectives.Systems;

namespace Content.Server.Imperial.XxRaay.Systems;

/// <summary>
/// Серверная система целей девиантных андроидов
/// </summary>
public sealed class AndroidDeviantAntagSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AndroidDeviantSaveBrethrenConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(EntityUid uid, AndroidDeviantSaveBrethrenConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        var total = 0;
        var deviant = 0;

        var query = EntityQueryEnumerator<AndroidStressComponent>();
        while (query.MoveNext(out _, out var stress))
        {
            if (!stress.CanBeDeviant)
                continue;

            total++;
            if (stress.IsDeviant)
                deviant++;
        }

        if (total <= 0)
        {
            args.Progress = 0f;
            return;
        }

        args.Progress = (float) deviant / total;
    }
}

