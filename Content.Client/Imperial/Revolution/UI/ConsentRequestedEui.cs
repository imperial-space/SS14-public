using System.Numerics;
using Content.Client.Eui;
using Content.Shared.Imperial.Revolutionary;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client.Imperial.Revolution.UI
{
    /// <summary>
    /// Клиентский UI для окна запроса согласия на обращение в революционеры.
    /// </summary>
    [UsedImplicitly]
    public sealed class ConsentRequestedEui : BaseEui
    {
        private readonly ConsentRequestedMenu _consentWindow;

        public ConsentRequestedEui()
        {
            _consentWindow = new ConsentRequestedMenu();

            _consentWindow.OnDeny += () =>
            {
                SendMessage(new ConsentRequestedEuiMessage(false));
                _consentWindow.Close();
            };

            _consentWindow.OnClose += () => SendMessage(new ConsentRequestedEuiMessage(false));

            _consentWindow.OnAccept += () =>
            {
                SendMessage(new ConsentRequestedEuiMessage(true));
                _consentWindow.Close();
            };
        }

        public override void HandleState(EuiStateBase state)
        {
            if (state is ConsentRequestedState consentState)
            {
                _consentWindow.SetConverterName(consentState.ConverterName);
            }
        }

        public override void Opened()
        {
            IoCManager.Resolve<IClyde>().RequestWindowAttention();
            _consentWindow.OpenCenteredAt(new Vector2(0.5f, 0.5f));
        }

        public override void Closed()
        {
            _consentWindow.Close();
        }
    }
}
