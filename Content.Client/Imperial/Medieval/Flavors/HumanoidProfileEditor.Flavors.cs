
using Content.Client.Imperial.Medieval.Flavors;
using Content.Shared.Imperial.ICCVar;
using Content.Shared.Players.PlayTimeTracking;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    public byte[]? FlavorImage;
    private FlavorHelpWindow? _helpWindow;
    private FlavorExamineWindow? _testWindow;
    public void UpdateFlavorHandle()
    {
        if (_flavorText == null)
            return;
        var session = _playerManager.LocalSession;
        if (session == null)
            return;

        var playtimes = _playtime.GetPlayTimes(session);
        var time = TimeSpan.Zero;
        playtimes.TryGetValue(PlayTimeTrackingShared.TrackerOverall, out time);
        var requiredTime = _cfgManager.GetCVar(ICCVars.FlavorPlaytimeRequirement);

        if (time < TimeSpan.FromSeconds(requiredTime))
        {
            _flavorText.FlavorImage.Disabled = true;
            _flavorText.FlavorImage.ToolTip = Loc.GetString("imperial-medieval-flavor-cant", ("hours", Math.Round((requiredTime - time.TotalSeconds) / 60 / 60, 1)));
        }
        else
        {
            _flavorText.OnFlavorImageChanged += OnImageSelected;
            _flavorText.OnTestPressed += OnTestPressed;
            _flavorText.FlavorImage.ToolTip = null;
            _flavorText.FlavorImage.Disabled = false;
        }
        _flavorText.OnHelpPressed += OnHelpPressed;
    }
    public void OnImageSelected(byte[] image)
    {
        if (_flavorText == null)
            return;

        FlavorImage = image;
        _flavorText.FlavorImage.TextureNormal = _flavors.GetImageFromByteArray(image).texture;
        IsDirty = true;
        UpdateSaveButton();
    }
    public void SetFlavorImage(byte[]? image)
    {
        if (_flavorText == null)
            return;

        FlavorImage = image;
        _flavorText.FlavorImage.TextureNormal = _flavors.GetImageFromByteArray(image).texture;
    }
    public void OnHelpPressed()
    {
        if (_helpWindow != null)
            _helpWindow.Close();

        _helpWindow = new();
        _helpWindow.OpenCentered();
    }
    public void OnTestPressed()
    {
        if (_testWindow != null)
            _testWindow.Close();

        if (Profile == null)
            return;

        _testWindow = new(Profile.FlavorText, FlavorImage);
        _testWindow.OpenCentered();
    }
}
