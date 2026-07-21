using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class RatlingAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ReplacementAccentSystem _replacement = default!;


    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RatlingAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    public string Accentuate(string message, RatlingAccentComponent component)
    {
        var msg = message;

        // Suffix:
        if (_random.Prob(component.suffixChance))
        {
            int randomNumber = _random.Next(1, 5);
            msg += (" " + Loc.GetString("ratling-suffix-" + randomNumber)); // e.g. "We only want cheese Peep!"
        }
        return msg;
    }

    private void OnAccentGet(EntityUid uid, RatlingAccentComponent component, AccentGetEvent args)
    {
        args.Message = Accentuate(args.Message, component);
    }
}
