using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Medieval.DoOnUse.DoAfter;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.DoOnUse.DoAfter;

public sealed partial class MedievalDoAfterSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageableSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MedievalDoAfterEveryComponent, GetVerbsEvent<AlternativeVerb>>(GenerateDoAfter);

        SubscribeLocalEvent<MedievalDoAfterEveryComponent, MedievalHitOnDoAfter>(GiveHit);
    }
    private void GiveHit(EntityUid uid, MedievalDoAfterEveryComponent comp, MedievalHitOnDoAfter ev)
    {
        if (ev.Cancelled || !TryComp<DamageableComponent>(uid, out var damagecomp)) return;
        DamageSpecifier damage = new()
        {
            DamageDict = new()
            {
                { comp.TypeHit, comp.NumHit }
            }
        };
        _damageableSystem.TryChangeDamage(uid, damage, true, false);
        if (TryComp<DamageableComponent>(ev.Target, out var damageable))
            Dirty(ev.Target.Value, damageable);
    }
    private void StartDoAfterHit(MedievalDoAfterEveryComponent comp, GetVerbsEvent<AlternativeVerb> ev)
    {
        var doAfterHit = new DoAfterArgs(EntityManager, ev.User, comp.Time, new MedievalHitOnDoAfter(), ev.Target, ev.User)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            CancelDuplicate = true
        };
        _doAfter.TryStartDoAfter(doAfterHit);
    }
    private void GenerateDoAfter(EntityUid uid, MedievalDoAfterEveryComponent comp, GetVerbsEvent<AlternativeVerb> ev)
    {
        if (!ev.CanAccess || !ev.CanInteract || ev.User == ev.Target)
            return;
        ev.Verbs.Add(new AlternativeVerb
        {
            Act = () =>
            {
                switch (comp.Type)
                {
                    case TypeMedievalDoAfter.Hit:
                        {
                            StartDoAfterHit(comp, ev);
                            break;
                        }
                    default:
                        break;
                }
            },
            Text = Loc.GetString(comp.NameLocId)
        }
        );
    }
}
