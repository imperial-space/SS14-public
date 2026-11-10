// Imperial Weekly Mode Start
using Content.Client.Imperial.WeeklyMode.Lathe;
// Imperial Weekly Mode End
using Content.Shared.Lathe;
using Content.Shared.Research.Components;
using Content.Shared.PrinterDoc; // Imperial PrinterDoc
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Lathe.UI
{
    [UsedImplicitly]
    public sealed class LatheBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        // Imperial Weekly Mode: Original code:
        // private LatheMenu? _menu;
        // Imperial Weekly Mode Start
        private WeeklyLatheMenu? _menu;
        // Imperial Weekly Mode End
        public LatheBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            // Imperial Weekly Mode: Original code:
            // _menu = this.CreateWindowCenteredRight<LatheMenu>();
            // Imperial Weekly Mode Start

            _menu = this.CreateWindowCenteredRight<WeeklyLatheMenu>();
            // Imperial Weekly Mode End
            _menu.SetEntity(Owner);

            _menu.OnServerListButtonPressed += _ =>
            {
                SendMessage(new ConsoleServerSelectionMessage());
            };

            _menu.RecipeQueueAction += (recipe, amount) =>
            {
                SendMessage(new LatheQueueRecipeMessage(recipe, amount));
            };

            // Imperial PrinterDoc Start
            _menu.OnUseCardIdCheckBoxChanged += useCardId =>
            {
                SendMessage(new PrinterDocCheckIdCardMessage(useCardId));
            };
            // Imperial PrinterDoc End

            _menu.QueueDeleteAction += index => SendMessage(new LatheDeleteRequestMessage(index));
            _menu.QueueMoveUpAction += index => SendMessage(new LatheMoveRequestMessage(index, -1));
            _menu.QueueMoveDownAction += index => SendMessage(new LatheMoveRequestMessage(index, 1));
            _menu.DeleteFabricatingAction += () => SendMessage(new LatheAbortFabricationMessage());
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            // Imperial PrinterDoc
            if (_menu == null)
                return;
            // Imperial Weekly Mode: Original code:
            // if (state is LatheUpdateState msg)
            // Imperial Weekly Mode Start
            if (state is WeeklyLatheUpdateState msg)
            // Imperial Weekly Mode End
            {
                _menu.Recipes = msg.Recipes;
                // Imperial Weekly Mode Start
                _menu.WeeklyRecipes = msg.WeeklyRecipes;
                // Imperial Weekly Mode End
                _menu.PopulateRecipes();
                _menu.UpdateCategories();
                _menu.PopulateQueueList(msg.Queue);
                // Imperial Weekly Mode: Original code:
                // _menu.SetQueueInfo(msg.CurrentlyProducing);
                // Imperial Weekly Mode Start
                _menu.SetQueueInfo(msg.CurrentlyProducing, msg.CurrentlyProducingIsWeekly);
                // Imperial Weekly Mode End
                _menu.SetUseCardId(msg.UseCardId);
            }
        }
    }
}
