/*
 * Project: raincidation
 * File: RDControl.cs
 * License: All rights reserved
 * Copyright: (c) 2025 TornadoTechnology
 *
 * For the full license text, see the LICENSE file in the project root.
 * Link: https://github.com/Rainlucid/raincidation
 */

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RD.UI;

public abstract class RDControl : Control
{
    public Color? BackgroundColor;

    public void SetAnchorPreset(LayoutContainer.LayoutPreset preset)
    {
        LayoutContainer.SetAnchorPreset(this, preset);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (BackgroundColor is not null)
            handle.DrawRect(new UIBox2(Vector2.Zero, Size), BackgroundColor.Value);
    }
}
