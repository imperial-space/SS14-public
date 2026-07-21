using System.Collections.Generic;
using System.Numerics;

namespace Content.Client.Imperial.Medieval.Achievements.UI;

public sealed class AchievementTreeViewState
{
    public string? CurrentTab;

    public readonly Dictionary<string, TabView> Views = new();

    public sealed class TabView
    {
        public Vector2 Pan;
        public float Zoom = 1f;
    }
}
