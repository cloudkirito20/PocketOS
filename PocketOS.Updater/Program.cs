namespace PocketOS.Updater;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("PocketOS.Updater.exe <source> <target> <restart>");
            return;
        }

        string source = args[0];
        string target = args[1];
        string restart = args[2];

        var engine = new UpdateEngine();

        await engine.ApplyUpdateAsync(
            source,
            target,
            restart);
    }
}
