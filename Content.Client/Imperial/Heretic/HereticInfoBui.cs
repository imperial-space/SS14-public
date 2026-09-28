using System;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticInfoBui : BoundUserInterface
{
    private HereticInfoWindow? _window;

    public HereticInfoBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        EnsureWindow();
        AnimateBook(opening: true);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not HereticInfoBuiState infoState)
            return;

        EnsureWindow();
        _window?.Populate(infoState);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            AnimateBook(opening: false);
        base.Dispose(disposing);
    }

    private void EnsureWindow()
    {
        if (_window != null)
            return;

        _window = this.CreateWindow<HereticInfoWindow>();
        _window.OnPathSelected += id => SendMessage(new HereticSelectPathMessage { KnowledgeId = id });
        _window.OnKnowledgeSelected += id => SendMessage(new HereticResearchKnowledgeMessage { KnowledgeId = id });
        _window.OnDenyAscension += () => SendMessage(new HereticDenyAscensionMessage());
    }

    private void AnimateBook(bool opening)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();

        // HereticKnowledgeHolder is parented to the heretic entity
        if (!entMan.TryGetComponent<TransformComponent>(Owner, out var xform))
            return;
        var hereticUid = xform.ParentUid;
        if (hereticUid == EntityUid.Invalid)
            return;

        // Find the book in heretic's held items
        EntityUid? bookEnt = null;
        var hands = entMan.System<SharedHandsSystem>();
        foreach (var held in hands.EnumerateHeld(hereticUid))
        {
            if (entMan.HasComponent<HereticMansusBookComponent>(held))
            {
                bookEnt = held;
                break;
            }
        }

        if (!bookEnt.HasValue)
            return;
        if (!entMan.TryGetComponent<SpriteComponent>(bookEnt.Value, out var sprite))
            return;

        var book = bookEnt.Value;

        if (opening)
        {
            sprite.LayerSetState(0, "book_opening");
            // book_opening: 21 frames × 0.1s = 2.1s
            Timer.Spawn(TimeSpan.FromSeconds(2.1), () =>
            {
                if (entMan.TryGetComponent<SpriteComponent>(book, out var s))
                    s.LayerSetState(0, "book_open");
            });
        }
        else
        {
            sprite.LayerSetState(0, "book_closing");
            // book_closing: 17 frames × 0.1s = 1.7s
            Timer.Spawn(TimeSpan.FromSeconds(1.7), () =>
            {
                if (entMan.TryGetComponent<SpriteComponent>(book, out var s))
                    s.LayerSetState(0, "book");
            });
        }
    }
}
