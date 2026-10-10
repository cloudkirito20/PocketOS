namespace PocketOS.Manager.Services;

public class UpdateScheduler
{
    private Timer? _timer;

    public void Start()
    {
        _timer = new Timer(async _ =>
        {
            var service = new UpdateService();
            await service.CheckForUpdatesAsync();
        },
        null,
        TimeSpan.FromSeconds(10),
        TimeSpan.FromHours(24));
    }

    public void Stop()
    {
        _timer?.Dispose();
    }
}
