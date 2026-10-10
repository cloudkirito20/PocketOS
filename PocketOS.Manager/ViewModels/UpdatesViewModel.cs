namespace PocketOS.Manager.ViewModels;

public partial class UpdatesViewModel
{
    public string StatusMessage { get; set; }
        = "Ready";

    public void SetStatus(string message)
    {
        StatusMessage = message;
    }
}
