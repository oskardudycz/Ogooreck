module BankAccountTests

open System
open Deciders.BankAccount
open Ogooreck.BusinessLogic
open Deciders.BankAccountDecider
open Xunit


let random = Random()
let now = DateTimeOffset.UtcNow

let getNow = fun () -> now

let spec =
    Specification.For(decide getNow, evolve)

let BankAccountOpenedWith =
    fun bankAccountId now version ->
        let accountNumber = Guid.NewGuid().ToString()

        let clientId = Guid.NewGuid()
        let currencyISOCode = "USD"

        { BankAccountId = bankAccountId
          AccountNumber = accountNumber
          ClientId = clientId
          CurrencyISOCode = currencyISOCode
          CreatedAt = now
          Version = version }
        |> Event.BankAccountOpened

let BankAccountClosedWith =
    fun bankAccountId now version ->
        { BankAccountId = bankAccountId
          Reason = Guid.NewGuid().ToString()
          ClosedAt = now
          Version = version }
        |> Event.BankAccountClosed

[<Fact>]
let ``GIVEN non existing bank account WHEN open with valid params THEN bank account is opened`` () =
    let bankAccountId = Guid.NewGuid()

    let accountNumber =
        Guid.NewGuid().ToString()

    let clientId = Guid.NewGuid()
    let currencyISOCode = "USD"

    spec
        .Given()
        .When(
            { BankAccountId = bankAccountId
              AccountNumber = accountNumber
              ClientId = clientId
              CurrencyISOCode = currencyISOCode }
            |> Command.OpenBankAccount
        )
        .Then(
            { BankAccountId = bankAccountId
              AccountNumber = accountNumber
              ClientId = clientId
              CurrencyISOCode = currencyISOCode
              CreatedAt = now
              Version = 1 }
            |> Event.BankAccountOpened
        )

[<Fact>]
let ``GIVEN open bank account WHEN record deposit with valid params THEN deposit is recorded`` () =
    let bankAccountId = Guid.NewGuid()
    let amount = decimal (random.NextDouble())
    let cashierId = Guid.NewGuid()

    spec
        .Given(BankAccountOpenedWith bankAccountId now 1)
        .When(
            { Amount = amount
              CashierId = cashierId }
            |> Command.RecordDeposit
        )
        .Then(
            { BankAccountId = bankAccountId
              Amount = amount
              CashierId = cashierId
              RecordedAt = now
              Version = 1 }
            |> Event.DepositRecorded
        )

[<Fact>]
let ``GIVEN closed bank account WHEN record deposit with valid params THEN fails with invalid operation exception`` () =
    let bankAccountId = Guid.NewGuid()

    let amount = decimal (random.NextDouble())
    let cashierId = Guid.NewGuid()

    spec
        .Given(
            BankAccountOpenedWith bankAccountId now 1,
            BankAccountClosedWith bankAccountId now 1
        )
        .When(
            { Amount = amount
              CashierId = cashierId }
            |> Command.RecordDeposit
        )
        .ThenThrows<InvalidOperationException>
