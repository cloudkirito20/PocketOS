using System.ComponentModel;
using PocketOS.Bridge.Partner;

namespace PocketOS.Manager.ViewModels;

public class HomeViewModel : INotifyPropertyChanged
{
    public PartnerStatus Partner { get; private set; }
        = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdatePartner(PartnerStatus status)
    {
        Partner = status;
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(nameof(Partner)));
    }
}
