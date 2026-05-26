namespace ErgStream.Platforms.Windows;

public class BeepService : IBeepService
{
    /// <summary>
    /// Plays a beep for the specified duration using Console.Beep.
    /// The call is offloaded to a thread pool thread so it does not block the UI.
    /// </summary>
    public void Beep(BeepType type)
    {
        (int duration, int frequency) = type switch
        {
            BeepType.Prepare => (500, 1000),
            BeepType.Go => (700, 1500),
            BeepType.Stop => (700, 500),
            _ => (1000, 200)
        };

        Task.Run(() => Console.Beep(frequency, duration));
    }
}
