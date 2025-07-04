using System.Numerics;
using Content.Client.Lobby;
using Content.Shared.Preferences;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Medieval.CharacterBlock;

public sealed class CharacterBlockedGui : DefaultWindow
{
    public CharacterBlockedGui()
    {
        MinSize = SetSize = new Vector2(360, 360);

        Title = Loc.GetString("character-blocked-gui");

        var selectedCharacter = (HumanoidCharacterProfile)IoCManager.Resolve<IClientPreferencesManager>()
            .Preferences!.SelectedCharacter;

        var mainContainer = new BoxContainer()
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            HorizontalExpand = true
        };

        Contents.AddChild(mainContainer);

        var text = new RichTextLabel();

        text.HorizontalAlignment = HAlignment.Center;
        text.VerticalAlignment = VAlignment.Center;
        text.Text = Loc.GetString("character-blocked-gui-text", ("characterName", selectedCharacter.Name));

        Contents.AddChild(text);
    }
}
