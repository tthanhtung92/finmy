namespace Finmy.Budgeting.Application.Envelopes;

public static class BudgetingAlertPolicy
{
    public const decimal LowBalanceRatio = 0.2m;

    public static bool IsLowBalance(decimal allocated, decimal remaining)
    {
        return remaining < allocated * LowBalanceRatio && allocated > 0;
    }
}
