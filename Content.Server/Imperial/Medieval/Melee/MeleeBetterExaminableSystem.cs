using Content.Shared.Examine;
using Content.Shared.Weapons.Melee;
using Content.Shared.MedievalMeleeResource.Components;
using Content.Shared.MeleeParry.Components;
using Robust.Shared.Maths;
using Content.Server.MedievalPotionChecker.Components;
using Content.Shared.MedievalMeleeResource.Components;

namespace Content.Server.MeleeBetterExaminable
{
    public sealed partial class MeleeBetterExaminableSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MeleeWeaponComponent, ExaminedEvent>(OnExamine);

        }
        private void OnExamine(EntityUid uid, MeleeWeaponComponent component, ExaminedEvent args)
        {
            if (TryComp<MeleeWeaponComponent>(uid, out var weapon) && !HasComp<ExaminerComponent>(uid) && !HasComp<MedievalPotionCheckAbleComponent>(uid))
            {
                var totaldamage = 0;
                foreach (var damage in weapon.Damage.DamageDict)
                {
                    totaldamage += damage.Value.Int();
                }
                if (totaldamage <= 0)
                    return;
                args.PushMarkup("Скорость атаки [color=green]" + weapon.AttackRate + "[/color]");
                args.PushMarkup("Множитель урона при широкой атаке [color=yellow]" + "1" + "[/color]");
                args.PushMarkup("Множитель урона при точечной атаке [color=orange]" + weapon.ClickDamageModifier + "[/color]");
                args.PushMarkup("Дальность атаки [color=cyan]" + weapon.Range + "[/color]");
                if (weapon.ResetOnHandSelected)
                    args.PushMarkup("Это оружие [color=red]не может[/color] быть использовано как парное");
                else
                    args.PushMarkup("Это оружие [color=green]может[/color] быть использовано как парное");
            }
            if (!HasComp<MeleeParryComponent>(uid) && !HasComp<ExaminerComponent>(uid) && !HasComp<MedievalPotionCheckAbleComponent>(uid))
                args.PushMarkup("Шанс паррирования [color=red]0%[/color]");
            if (TryComp<MedievalMeleeResourceComponent>(uid, out var resource) && !HasComp<ExaminerComponent>(uid) && !HasComp<MedievalPotionCheckAbleComponent>(uid))
            {
                switch (resource.DamageState)
                {
                    case "Up":
                        args.PushMarkup("Это оружие [color=cyan]дополнительно заточенно[/color]");
                        break;
                    case "Full":
                        args.PushMarkup("Это оружие в [color=green]идеальном[/color] состоянии");
                        break;
                    case "AlmostFull":
                        args.PushMarkup("Это оружие выглядит [color=#90ee90]слегка поцарапанным[/color]");
                        break;
                    case "Damaged":
                        args.PushMarkup("На этом оружии видны [color=yellow]повреждения[/color]");
                        break;
                    case "BadlyDamaged":
                        args.PushMarkup("Это оружие в [color=orange]отвратном[/color] состоянии");
                        break;
                    case "Broken":
                        args.PushMarkup("Это оружие вот-вот [color=red]развалится[/color]");
                        break;
                    default:
                        //код, выполняемый если выражение не имеет ни одно из выше указанных значений
                        break;
                }
            }
        }
    }
}
