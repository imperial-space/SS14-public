using Content.Shared.Imperial.Medieval.Achievements;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Client.Imperial.Medieval.Achievements;

public sealed class AchievementSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<AchievementUnlockedEvent>(OnAchievementUnlocked);
        SubscribeNetworkEvent<AchievementMenuDataEvent>(OnMenuDataReceived);
    }

    private void OnAchievementUnlocked(AchievementUnlockedEvent ev, EntitySessionEventArgs args)
    {
        _uiManager.GetUIController<AchievementUIController>().QueueNotification(ev.AchievementId);
    }

    private void OnMenuDataReceived(AchievementMenuDataEvent ev, EntitySessionEventArgs args)
    {
        _uiManager.GetUIController<AchievementUIController>()
            .UpdateMenuData(ev.Unlocked, ev.GlobalPercents, ev.Progress);
    }

    public void RequestMenuData()
    {
        RaiseNetworkEvent(new RequestAchievementMenuDataEvent());
    }
}
