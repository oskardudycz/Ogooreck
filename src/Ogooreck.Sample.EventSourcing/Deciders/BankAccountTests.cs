using Ogooreck.BusinessLogic;
using Ogooreck.Sample.EventSourcing.Aggregates;
using System;
using FluentAssertions;

namespace Ogooreck.Sample.EventSourcing.Deciders;

using static BusinessLogicSpecification;

public class BankAccountTests
{
    private readonly Random random = new();
    private static readonly DateTimeOffset now = DateTimeOffset.UtcNow;

    private Func<object, BankAccount, object> decide =
        (command, bankAccount) => BankAccountDecider.Handle(() => now, command, bankAccount);

    [Fact]
    public void GivenNonExistingBankAccount_WhenOpenWithValidParams_ThenSucceeds()
    {
        var bankAccountId = Guid.NewGuid();
        var accountNumber = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid();
        var currencyISOCode = "USD";

        Given<BankAccount>()
            .When(state => decide(new OpenBankAccount(bankAccountId, accountNumber, clientId, currencyISOCode), state))
            .Then(EVENT(new BankAccountOpened(bankAccountId, accountNumber, clientId, currencyISOCode, now, 1)));
    }


    [Fact]
    public void GivenOpenBankAccount_WhenRecordDepositWithValidParams_ThenSucceeds()
    {
        var bankAccountId = Guid.NewGuid();
        var accountNumber = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid();
        var currencyISOCode = "USD";

        var amount = (decimal)random.NextDouble();
        var cashierId = Guid.NewGuid();

        Given<BankAccount>(BankAccount.Evolve,
                () => new BankAccountOpened(bankAccountId, accountNumber, clientId, currencyISOCode, now, 1))
            .When(state => decide(new RecordDeposit(amount, cashierId), state))
            .Then(EVENT(new DepositRecorded(bankAccountId, amount, cashierId, now, 2)));
    }

    [Fact]
    public void GivenClosedBankAccount_WhenRecordDepositWithValidParams_ThenFailsWithInvalidOperationException()
    {
        var bankAccountId = Guid.NewGuid();
        var accountNumber = Guid.NewGuid().ToString();
        var clientId = Guid.NewGuid();
        var currencyISOCode = "USD";

        var reason = Guid.NewGuid().ToString();

        var amount = (decimal)random.NextDouble();
        var cashierId = Guid.NewGuid();

        Given<BankAccount>(
                BankAccount.Evolve,
                () => new object[]
                {
                    new BankAccountOpened(bankAccountId, accountNumber, clientId, currencyISOCode, now, 1),
                    new BankAccountClosed(bankAccountId, reason, now, 2),
                }
            )
            .When(state => decide(new RecordDeposit(amount, cashierId), state))
            .ThenThrows<InvalidOperationException>(exception => exception.Message.Should().Be("Account is closed!"));
    }
}
