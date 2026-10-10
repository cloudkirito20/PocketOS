namespace PocketOS.Bridge.Partner;

public class PartnerService
{
    private readonly MoodEngine _mood =
        new();

    public PartnerStatus Update(
        PartnerStatus status)
    {
        var animation =
            _mood.Calculate(status);

        status.Mood =
            animation.ToString();

        return status;
    }
}
