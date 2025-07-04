/*
 * Project: raincidation
 * File: RDDeathScreenControl.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using Content.Client._RD.UI;
using Content.Client.Resources;
using Content.Shared._RD.DeathScreen;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._RD.DeathScreen;

public sealed class RDDeathScreenControl : RDControl
{
    private const float FadeInDuration = 1f;
    private const float FadeOutDuration = 9f;
    private const float DelayTime = 2f;

    [Dependency] private readonly IResourceCache _resourceCache = default!;

    public event Action? OnAnimationEnd;

    private readonly Label _label;

    private string _title = string.Empty;
    private string _reason = string.Empty;
    private float _elapsedTime;

    private AnimationPhase _phase = AnimationPhase.None;

    public RDDeathScreenControl()
    {
        IoCManager.InjectDependencies(this);

        SetAnchorPreset(LayoutContainer.LayoutPreset.Wide);

        _label = new Label
        {
            Text = _title,
            FontOverride = _resourceCache.GetFont("/Fonts/_RD/KosmoletFuturism.otf", 86),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            FontColorOverride = Color.DarkRed,
        };

        AddChild(_label);
    }

    public void AnimationStart(RDDeathScreenShowEvent ev)
    {
        _title = ev.Title;
        _reason = ev.Reason;

        _label.Text = _title;
        _elapsedTime = 0f;
        _phase = AnimationPhase.FadeIn;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        _elapsedTime += args.DeltaSeconds;

        switch (_phase)
        {
            case AnimationPhase.FadeIn:
            {
                var alpha = MathHelper.Clamp(_elapsedTime / FadeInDuration, 0f, 1f);
                BackgroundColor = Color.DarkRed.WithAlpha(alpha);

                if (_elapsedTime >= FadeInDuration)
                {
                    _elapsedTime = 0f;
                    _phase = AnimationPhase.FadeOut;
                }

                break;
            }

            case AnimationPhase.FadeOut:
            {
                var alpha = MathHelper.Clamp(1f - _elapsedTime / FadeOutDuration, 0f, 1f);
                BackgroundColor = Color.DarkRed.WithAlpha(alpha);

                if (_elapsedTime >= FadeOutDuration)
                    _phase = AnimationPhase.Delay;
                break;
            }

            case AnimationPhase.Delay:
            {
                BackgroundColor = Color.DarkRed.WithAlpha(0);
                if (_elapsedTime >= DelayTime)
                    _phase = AnimationPhase.Done;

                break;
            }

            case AnimationPhase.Done:
                OnAnimationEnd?.Invoke();
                break;
        }
    }

    private enum AnimationPhase
    {
        None,
        FadeIn,
        Delay,
        FadeOut,
        Done
    }
}
