namespace PocketOS.Core.Firmware;

public enum FlashResult
{
    Idle,
    Detecting,
    Connecting,
    Erasing,
    Writing,
    Verifying,
    Complete,
    Failed
}
