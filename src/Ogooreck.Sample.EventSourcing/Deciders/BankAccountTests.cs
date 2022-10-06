using Ogooreck.BusinessLogic;
using FluentAssertions;

namespace Ogooreck.Sample.EventSourcing.Deciders;

using static BankAccountEventsBuilder;

public class BankAccountTests
{
    private readonly Random random = new();
    private static readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    private readonly DeciderSpecification<BankAccount> Spec = Specification.For<BankAccount>(
        (command, bankAccount) => BankAccountDecider.Handle(() => now, command, bankAccount),
        BankAccount.Evolve
    );

    [Fact]
    public void GivenNonExistingBankAccount_WhenOpenWithValidParams_ThenSucceeds()
    {
        var bankAccountId = Guid.NewGuid();
        var accountNumber = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid();
        var currencyISOCode = "USD";

        Spec.Given()
            .When(new OpenBankAccount(bankAccountId, accountNumber, clientId, currencyISOCode))
            .Then(new BankAccountOpened(bankAccountId, accountNumber, clientId, currencyISOCode, now, 1));
    }

    [Fact]
    public void GivenOpenBankAccount_WhenRecordDepositWithValidParams_ThenSucceeds()
    {
        var bankAccountId = Guid.NewGuid();

        var amount = (decimal)random.NextDouble();
        var cashierId = Guid.NewGuid();

        Spec.Given(BankAccountOpened(bankAccountId, now, 1))
            .When(new RecordDeposit(amount, cashierId))
            .Then(new DepositRecorded(bankAccountId, amount, cashierId, now, 2));
    }

    [Fact]
    public void GivenClosedBankAccount_WhenRecordDepositWithValidParams_ThenFailsWithInvalidOperationException()
    {
        var bankAccountId = Guid.NewGuid();

        var amount = (decimal)random.NextDouble();
        var cashierId = Guid.NewGuid();

        Spec.Given(
                BankAccountOpened(bankAccountId, now, 1),
                BankAccountClosed(bankAccountId, now, 2)
            )
            .When(new RecordDeposit(amount, cashierId))
            .ThenThrows<InvalidOperationException>(exception => exception.Message.Should().Be("Account is closed!"));
    }
}

public static class BankAccountEventsBuilder
{
    public static BankAccountOpened BankAccountOpened(Guid bankAccountId, DateTimeOffset now, long version)
    {
        var accountNumber = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid();
        var currencyISOCode = "USD";

        return new BankAccountOpened(bankAccountId, accountNumber, clientId, currencyISOCode, now, version);
    }

    public static BankAccountClosed BankAccountClosed(Guid bankAccountId, DateTimeOffset now, long version)
    {
        var reason = Guid.NewGuid().ToString();

        return new BankAccountClosed(bankAccountId, reason, now, version);
    }
}
