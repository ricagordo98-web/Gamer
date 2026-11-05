namespace Gamer.Ui.Services;

public interface ISystemNotifier
{
    void Notify(string title, string message);
}

public class NoSystemNotifier : ISystemNotifier
{
    public void Notify(string title, string message)
    {
    }
}
