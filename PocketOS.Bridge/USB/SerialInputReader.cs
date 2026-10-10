using System.IO.Ports;

namespace PocketOS.Bridge.USB;

public class SerialInputReader
{
    private SerialPort? _port;


    public event Action<string>? ButtonReceived;


    public bool Connect(string portName)
    {
        try
        {
            _port = new SerialPort(
                portName,
                115200);

            _port.DataReceived += OnDataReceived;

            _port.Open();

            return true;
        }
        catch
        {
            return false;
        }
    }


    private void OnDataReceived(
        object sender,
        SerialDataReceivedEventArgs e)
    {
        var data =
            _port?.ReadLine();

        if (!string.IsNullOrWhiteSpace(data))
        {
            ButtonReceived?.Invoke(data.Trim());
        }
    }
}
