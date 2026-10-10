namespace PocketOS.Bridge.Input;

public static class ButtonMapping
{
    public static readonly Dictionary<string, string> Keys =
        new()
        {
            { "UP", "Up" },
            { "DOWN", "Down" },
            { "LEFT", "Left" },
            { "RIGHT", "Right" },
            { "A", "Enter" },
            { "B", "Backspace" },
            { "START", "Space" },
            { "SELECT", "Shift" }
        };
}
