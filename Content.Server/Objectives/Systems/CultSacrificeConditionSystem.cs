using Content.Server.Imperial.Cult.Components;
using Content.Server.Imperial.Cult;
using Content.Shared.Objectives.Components;

namespace Content.Server.Objectives.Systems;

/// <summary>
/// Отслеживает прогресс цели "принести N жертв через руну жертвоприношения".
/// Слушает <see cref="CultSacrificeCompletedEvent"/> и увеличивает счётчик
/// у всех целей с <see cref="CultSacrificeConditionComponent"/>.
/// </summary>
public sealed class CultSacrificeConditionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CultSacrificeConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        SubscribeLocalEvent<CultSacrificeCompletedEvent>(OnSacrificeCompleted);
    }

    private void OnGetProgress(EntityUid uid, CultSacrificeConditionComponent comp, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = comp.RequiredCount <= 0
            ? 1f
            : Math.Min((float) comp.CurrentCount / comp.RequiredCount, 1f);
    }

    private void OnSacrificeCompleted(CultSacrificeCompletedEvent ev)
    {
        var query = EntityQueryEnumerator<CultSacrificeConditionComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.CurrentCount < comp.RequiredCount)
                comp.CurrentCount++;
        }
    }
}
