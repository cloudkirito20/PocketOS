using System.IO.Ports;

namespace PocketOSManager.Firmware;

public sealed class Esp32Detector
{
    public string[] DetectPorts()
    {
        return SerialPort.GetPortNames();
    }
}
