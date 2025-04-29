using System.Diagnostics.CodeAnalysis;
using Robust.Client.Console;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.UI;


[Virtual]
public class CommandImperialButton : BaseImperialButton
{
    public string? Command { get; set; }

    public CommandImperialButton()
    {
        OnPressed += Execute;
    }

    protected virtual bool CanPress()
    {
        return string.IsNullOrEmpty(Command) ||
               IoCManager.Resolve<IClientConGroupController>().CanCommand(Command.Split(' ')[0]);
    }

    protected virtual void Execute(ButtonEventArgs obj)
    {
        if (!string.IsNullOrEmpty(Command))
            IoCManager.Resolve<IClientConsoleHost>().ExecuteCommand(Command);
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        if (args.Count != 2 || !args.TryGetValue("Text", out var text) || !args.TryGetValue("Command", out var command))
        {
            control = null;

            return false;
        }

        Command = command;

        Text = Loc.GetString(text);
        control = this;

        return true;
    }
}
