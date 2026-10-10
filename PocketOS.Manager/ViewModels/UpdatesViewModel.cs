using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PocketOS.Manager.ViewModels;

public class UpdatesViewModel : INotifyPropertyChanged
{
    public string ManagerVersion { get; set; } = "Checking...";
    public string BridgeVersion { get; set; } = "Checking...";
    public string ESP32Version { get; set; } = "Checking...";

    public string ESP32DeviceStatus { get; set; }
        = "USB device not detected";

    public string StatusMessage { get; set; }
        = "Ready";


    public event PropertyChangedEventHandler? PropertyChanged;


    protected void OnPropertyChanged(
        [CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(name));
    }
}
