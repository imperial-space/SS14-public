using Content.Client.Imperial.Chemistry;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.Imperial.Medieval.Chemistry;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.Chemistry
{
    [UsedImplicitly]
    public sealed class PotionBookBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private PotionBookWindow? _window;

        public PotionBookBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();
            _window = this.CreateWindow<PotionBookWindow>();
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is PotionBookUserInterfaceState cast)
                _window?.UpdateState(cast);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            _window?.Close();
        }
    }
}
