using Content.Shared.Preferences;

namespace Content.Shared.Imperial.Medieval.CharacterBlock;

public static class CharacterPorifileExtensions
{
    public static string BuildId(this HumanoidCharacterProfile profile)
    {
        return profile.Name + profile.Sex + profile.Species;
    }
}
