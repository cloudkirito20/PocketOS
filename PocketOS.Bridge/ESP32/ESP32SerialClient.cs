using System.IO.Ports;
using System.Text.Json;

namespace PocketOS.Bridge.ESP32;

public class ESP32SerialClient
{
    private SerialPort? _port;

    public bool Connect(string portName)
    {
        try
        {
            _port = new SerialPort(portName, 115200);
            _port.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }


    public void Send(object data)
    {
        if (_port == null || !_port.IsOpen)
            return;

        string json =
            JsonSerializer.Serialize(data);

        _port.WriteLine(json);
    }


    public void Disconnect()
    {
        _port?.Close();
    }
}
