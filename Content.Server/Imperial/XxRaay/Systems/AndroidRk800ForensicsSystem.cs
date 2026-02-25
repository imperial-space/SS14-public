using System.Linq;
using Content.Server.Forensics;
using Content.Shared.Examine;
using Content.Shared.Imperial.XxRaay.Components;
using Robust.Shared.Localization;

namespace Content.Server.Imperial.XxRaay.Systems;

/// <summary>
/// Позволяет андроиду RK800 видеть отпечатки пальцев в описании объектов
/// </summary>
public sealed class AndroidRk800ForensicsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ForensicsComponent, ExaminedEvent>(OnForensicsExamined);
    }

    private void OnForensicsExamined(EntityUid uid, ForensicsComponent component, ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!HasComp<AndroidRk800Component>(args.Examiner))
            return;

        if (component.Fingerprints.Count == 0)
        {
            args.PushMarkup(Loc.GetString("android-rk800-examine-no-fingerprints"));
            return;
        }

        const int maxShown = 5;
        var total = component.Fingerprints.Count;
        var recentPrints = component.Fingerprints.Take(maxShown);

        var key = total == 1
            ? "android-rk800-examine-fingerprints-one"
            : "android-rk800-examine-fingerprints-many";

        using (args.PushGroup(nameof(AndroidRk800ForensicsSystem)))
        {
            args.PushMarkup(Loc.GetString(key, ("count", total)));

            foreach (var print in recentPrints)
            {
                args.PushMarkup(Loc.GetString("android-rk800-examine-fingerprint-line", ("print", print)));
            }
        }
    }
}

