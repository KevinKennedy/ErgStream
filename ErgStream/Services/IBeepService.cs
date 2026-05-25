namespace ErgStream.Services;

public enum BeepType
{
    Prepare,
    Go,
    Stop,
}

public interface IBeepService
{
    /// <summary>
    /// Plays a beep of the given type.
    /// </summary>
    void Beep(BeepType type);
}
