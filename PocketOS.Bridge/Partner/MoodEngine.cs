namespace PocketOS.Bridge.Partner;

public class MoodEngine
{
    public AnimationState Calculate(
        PartnerStatus status)
    {
        if (status.HP <= 20)
            return AnimationState.LowHP;

        if (status.State == "Shiny")
            return AnimationState.Shiny;

        if (status.State == "Battle")
            return AnimationState.Battle;

        return AnimationState.Idle;
    }
}
