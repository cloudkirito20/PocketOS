namespace PocketOS.Updater;

public class UpdateEngine
{
    private readonly FileReplacer _replacer = new();
    private readonly RestartManager _restart = new();

    public async Task ApplyUpdateAsync(
        string source,
        string target,
        string restartApplication)
    {
        await Task.Delay(1000);

        _replacer.ReplaceDirectory(
            source,
            target);

        _restart.Restart(
            restartApplication);
    }
}
