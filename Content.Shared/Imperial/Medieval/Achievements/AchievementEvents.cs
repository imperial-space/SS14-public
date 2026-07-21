using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Achievements;

[Serializable, NetSerializable]
public sealed class RequestAchievementMenuDataEvent : EntityEventArgs { }

[Serializable, NetSerializable]
public sealed class AchievementMenuDataEvent : EntityEventArgs
{
    public HashSet<string> Unlocked;
    public Dictionary<string, float> GlobalPercents;

    public Dictionary<string, Dictionary<string, int>> Progress;

    public AchievementMenuDataEvent(
        HashSet<string> unlocked,
        Dictionary<string, float> globalPercents,
        Dictionary<string, Dictionary<string, int>> progress)
    {
        Unlocked = unlocked;
        GlobalPercents = globalPercents;
        Progress = progress;
    }
}

[Serializable, NetSerializable]
public sealed class AchievementUnlockedEvent : EntityEventArgs
{
    public string AchievementId;

    public AchievementUnlockedEvent(string achievementId)
    {
        AchievementId = achievementId;
    }
}
