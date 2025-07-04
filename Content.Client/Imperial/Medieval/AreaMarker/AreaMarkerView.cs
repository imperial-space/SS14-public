using Robust.Client.Animations;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Animations;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.AreaMarker;

public sealed class AreaMarkerView : UIWidget
{
    private readonly AreaMarkerUiController _areaMarkerUiController = default!;

    private RichTextLabel _areaLabel = null!;
    private Animation _animation = null!;

    private const string AnimationName = "fade";

    private readonly Color _invisibleColor = new(1f, 1f, 1f, 0f);
    private readonly Color _visibleColor = new(1f, 1f, 1f, 1f);
    public AreaMarkerView()
    {
        IoCManager.InjectDependencies(this);

        _areaMarkerUiController = UserInterfaceManager.GetUIController<AreaMarkerUiController>();

        InitializeLabel();
        CreateAnimation();
        _areaMarkerUiController.AreaEntered += OnAreaEntered;
    }

    private void CreateAnimation()
    {
        _animation = new Animation
        {
            Length = TimeSpan.FromSeconds(14.5f),
            AnimationTracks =
            {
                new AnimationTrackControlProperty
                {
                    Property = nameof(Modulate),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(_invisibleColor, 0f),
                        new AnimationTrackProperty.KeyFrame(_visibleColor, 3.0f),
                        new AnimationTrackProperty.KeyFrame(_visibleColor, 1.5f),
                        new AnimationTrackProperty.KeyFrame(_invisibleColor, 3.5f),
                    },
                },
            },
        };
    }
    private void InitializeLabel()
    {
        _areaLabel = new RichTextLabel();
        _areaLabel.Visible = true;
        _areaLabel.Modulate = _invisibleColor;
        _areaLabel.SetWidth = 450;

        AddChild(_areaLabel);
    }

    private void OnAreaEntered(string message)
    {
        if (_areaLabel.HasRunningAnimation(AnimationName))
        {
            _areaLabel.Modulate = _invisibleColor;
            _areaLabel.StopAnimation(AnimationName);
        }

        var formatted = new FormattedMessage();
        formatted.AddMarkupOrThrow(message);

        _areaLabel.SetMessage(formatted);
        _areaLabel.PlayAnimation(_animation, AnimationName);
    }

    protected override void Deparented()
    {
        base.Deparented();
        _areaMarkerUiController.AreaEntered -= OnAreaEntered;
    }
}
