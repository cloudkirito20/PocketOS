using PocketOS.Bridge.Partner;

namespace PocketOS.Manager.Services;

public class CompanionSyncService
{
    public PartnerStatus? CurrentPartner { get; private set; }

    public void Sync(PartnerStatus status)
    {
        CurrentPartner = status;
    }
}
