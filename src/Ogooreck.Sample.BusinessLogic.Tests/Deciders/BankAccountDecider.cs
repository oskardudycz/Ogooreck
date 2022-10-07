namespace Ogooreck.Sample.BusinessLogic.Deciders;

public record OpenBankAccount(
    Guid BankAccountId,
    string AccountNumber,
    Guid ClientId,
    string CurrencyISOCode
);

public record RecordDeposit(
    decimal Amount,
    Guid CashierId
);

public record WithdrawnCashFromATM(
    decimal Amount,
    Guid AtmId
);

public record CloseBankAccount(
    string Reason
);

public static class BankAccountDecider
{

    public static object Handle(
        Func<DateTimeOffset> now,
        object command,
        BankAccount bankAccount
        ) =>
        command switch
        {
            OpenBankAccount openBankAccount =>
                Handle(now, openBankAccount),
            RecordDeposit recordDeposit =>
                Handle(now, recordDeposit, bankAccount),
            WithdrawnCashFromATM withdrawnCash =>
                Handle(now, withdrawnCash, bankAccount),
            CloseBankAccount closeBankAccount =>
                Handle(now, closeBankAccount, bankAccount),
            _ =>
                throw new InvalidOperationException($"{command.GetType().Name} cannot be handled for Bank Account")
        };

    private static BankAccountOpened Handle(
        Func<DateTimeOffset> now,
        OpenBankAccount command
    ) =>
        new BankAccountOpened(
            command.BankAccountId,
            command.AccountNumber,
            command.ClientId,
            command.CurrencyISOCode,
            now(),
            1
        );

    private static DepositRecorded Handle(
        Func<DateTimeOffset> now,
        RecordDeposit command,
        BankAccount account
    )
    {
        if (account.Status == BankAccountStatus.Closed)
            throw new InvalidOperationException("Account is closed!");

        return new DepositRecorded(account.Id, command.Amount, command.CashierId, now(), account.Version + 1);
    }

    private static CashWithdrawnFromATM Handle(
        Func<DateTimeOffset> now,
        WithdrawnCashFromATM command,
        BankAccount account
    )
    {
        if (account.Status == BankAccountStatus.Closed)
            throw new InvalidOperationException("Account is closed!");

        if (account.Balance < command.Amount)
            throw new InvalidOperationException("Not enough money!");

        return new CashWithdrawnFromATM(account.Id, command.Amount, command.AtmId, now(), account.Version + 1);
    }

    private static  BankAccountClosed Handle(
        Func<DateTimeOffset> now,
        CloseBankAccount command,
        BankAccount account
    )
    {
        if (account.Status == BankAccountStatus.Closed)
            throw new InvalidOperationException("Account is already closed!");

        return new BankAccountClosed(account.Id, command.Reason, now(), account.Version + 1);
    }
}
