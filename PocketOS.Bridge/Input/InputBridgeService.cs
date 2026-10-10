namespace PocketOS.Bridge.Input;

public class InputBridgeService
{
    public event Action<string>? KeyPressed;


    public void Process(
        ControllerInput input)
    {
        if (!input.Pressed)
            return;


        if (ButtonMapping.Keys.TryGetValue(
            input.Button,
            out var key))
        {
            KeyPressed?.Invoke(key);
        }
    }
}
