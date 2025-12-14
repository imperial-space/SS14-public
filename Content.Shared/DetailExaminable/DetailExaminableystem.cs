using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Imperial.Medieval.Flavors;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.DetailExaminable;

public sealed class DetailExaminableSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly SharedFlavorManager _flavors = default!; // Imperial Medieval Flavor Images

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DetailExaminableComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    private void OnGetExamineVerbs(Entity<DetailExaminableComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        // Imperial Medieval Flavor Images Comment Begin
        //if (Identity.Name(args.Target, EntityManager) != MetaData(args.Target).EntityName)
        //    return;
        // Imperial Medieval Flavor Images Comment End

        var detailsRange = _examine.IsInDetailsRange(args.User, ent);

        var user = args.User;

        var verb = new ExamineVerb
        {
            Act = () =>
            {
                // Imperial Medieval Flavor Images Begin
                if (_flavors.TryExamine(user, ent))
                    return;

                // Imperial Medieval Flavor Images End
                var markup = new FormattedMessage();
                markup.AddMarkupPermissive(ent.Comp.Content);
                _examine.SendExamineTooltip(user, ent, markup, false, false);
            },
            Text = Loc.GetString("detail-examinable-verb-text"),
            Category = VerbCategory.Examine,
            Disabled = !detailsRange,
            Message = detailsRange ? null : Loc.GetString("detail-examinable-verb-disabled"),
            Icon = new SpriteSpecifier.Texture(new ("/Textures/Interface/VerbIcons/examine.svg.192dpi.png"))
        };

        args.Verbs.Add(verb);
    }
}
