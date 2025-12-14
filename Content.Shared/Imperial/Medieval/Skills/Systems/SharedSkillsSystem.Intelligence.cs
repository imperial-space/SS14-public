using System.Windows.Input;
using Content.Shared.Examine;
using Content.Shared.Imperial.Medieval.Construction;
using Content.Shared.Imperial.Medieval.Illitid;
using Content.Shared.Paper;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.Skills;

public abstract partial class SharedSkillsSystem
{
    public const string IntelligenceId = "Intelligence";

    private void InitializeIntelligence()
    {
        SubscribeLocalEvent<SkillsComponent, GetConstructionSpeedModifiersEvent>(OnGetConstructionSpeedModifiers);
        SubscribeLocalEvent<SkillsComponent, PaperWriteAttemptEvent>(OnCanWrite);
        SubscribeLocalEvent<SkillsComponent, GetVerbsEvent<ExamineVerb>>(OnSkillsExamined);
    }

    private void OnGetConstructionSpeedModifiers(EntityUid uid, SkillsComponent comp, ref GetConstructionSpeedModifiersEvent args)
    {
        var (proto, level) = GetSkill(uid, IntelligenceId);

        if (level == 10)
            return;

        var diff = Math.Abs(level - 10);

        args.Modifier += (level > 10 ? proto.Modifiers["PositiveConstructionSpeedModifier"] : proto.Modifiers["NegativeConstructionSpeedModifier"]) * diff;
    }

    private void OnCanWrite(EntityUid uid, SkillsComponent comp, ref PaperWriteAttemptEvent args)
    {
        if (CanRead(uid))
            return;

        args.Cancelled = true;
        args.FailReason = "Вы слишком глупы.";
    }


    private void OnSkillsExamined(EntityUid uid, SkillsComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!_player.TryGetSessionByEntity(uid, out var session))
            return;

        var (_, self) = GetSkill(args.User, IntelligenceId);
        var (_, otherLevel) = GetSkill(uid, IntelligenceId);

        // в идеале конечно для резонатов перенести в их систему, ведь иначе будет путаница, но
        if ((self >= 20 || HasComp<IllitidComponent>(args.User)) && otherLevel < 14)
        {
            var verb = new ExamineVerb
            {
                Act = () =>
                {
                    if (_netMan.IsClient)
                        return;

                    var ev = new GetEnteredChatMessageMessage(GetNetEntity(uid), GetNetEntity(args.User));
                    RaiseNetworkEvent(ev, session);
                },
                Text = Loc.GetString("Заглянуть в голову"),
                Category = VerbCategory.Examine,
                Disabled = !args.CanAccess,
                Message = args.CanAccess ? null : Loc.GetString("detail-examinable-verb-disabled"),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Imperial/Medieval/Interface/Brain.png"))
            };
            args.Verbs.Add(verb);
        }
    }

    public bool CanRead(EntityUid uid)
    {
        var (_, level) = GetSkill(uid, IntelligenceId);

        if (level < 5)
            return false;

        return true;
    }
    public bool CanOpenDoorKey(EntityUid uid)
    {
        var (_, level) = GetSkill(uid, IntelligenceId);

        if (level > 5)
            return false;

        return true;
    }

    public bool IntelligenceMin(EntityUid uid)
    {
        var (_, level) = GetSkill(uid, IntelligenceId);

        if (level == 1)
            return true;

        return false;
    }
}
