using Content.Client.CharacterInfo;
using Content.Client.Imperial.Heretic.UI;
using Content.Shared.Imperial.Heretic.Components;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticCharacterInfoSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CharacterInfoSystem.GetCharacterInfoControlsEvent>(OnGetControls);
    }

    private void OnGetControls(ref CharacterInfoSystem.GetCharacterInfoControlsEvent ev)
    {
        if (!HasComp<HereticComponent>(ev.Entity))
            return;

        ev.Controls.Add(new HereticMansusControl());
    }
}
