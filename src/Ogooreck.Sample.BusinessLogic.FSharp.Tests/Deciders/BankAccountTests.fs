module BankAccountTests

open System
open Deciders.BankAccount
open Deciders.BankAccountPrimitives
open FsCheck
open Ogooreck.BusinessLogic
open Deciders.BankAccountDecider
open FsCheck.Xunit

let random = Random()
let now = DateTimeOffset.UtcNow

let spec =
    Specification.For(decide, evolve, (fun () -> NotInitialised))

let BankAccountOpenedWith =
    fun bankAccountId now version ->
        let accountNumber =
            AccountNumber.newNumber (Guid.NewGuid().ToString())

        let clientId = ClientId.newId ()

        let currencyISOCode =
            CurrencyCode.newCode "USD"

        BankAccountOpened
            { BankAccountId = bankAccountId
              AccountNumber = accountNumber
              ClientId = clientId
              CurrencyISOCode = currencyISOCode
              CreatedAt = now
              Version = version }

let BankAccountClosedWith =
    fun bankAccountId now version ->
        BankAccountClosed
            { BankAccountId = bankAccountId
              Reason = Guid.NewGuid().ToString()
              ClosedAt = now
              Version = version }

[<Property>]
let ``GIVEN non existing bank account WHEN open with valid params THEN bank account is opened``
    bankAccountId
    accountNumber
    clientId
    currencyISOCode
    =
    let notExistingAccount = Array.empty<_>

    spec
        .Given(notExistingAccount)
        .When(
            OpenBankAccount
                { BankAccountId = bankAccountId
                  AccountNumber = accountNumber
                  ClientId = clientId
                  CurrencyISOCode = currencyISOCode
                  Now = now }
        )
        .Then(
            BankAccountOpened
                { BankAccountId = bankAccountId
                  AccountNumber = accountNumber
                  ClientId = clientId
                  CurrencyISOCode = currencyISOCode
                  CreatedAt = now
                  Version = 1 }
        )
    |> ignore

[<Property>]
let ``GIVEN open bank account WHEN record deposit with valid params THEN deposit is recorded``
    bankAccountId
    amount
    cashierId
    =
    spec
        .Given(BankAccountOpenedWith bankAccountId now 1)
        .When(
            RecordDeposit
                { Amount = amount
                  CashierId = cashierId
                  Now = now }
        )
        .Then(
            DepositRecorded
                { BankAccountId = bankAccountId
                  Amount = amount
                  CashierId = cashierId
                  RecordedAt = now
                  Version = 2 }
        )
    |> ignore

[<Property>]
let ``GIVEN closed bank account WHEN record deposit with valid params THEN fails with invalid operation exception``
    bankAccountId
    amount
    cashierId
    =
    spec
        .Given(
            BankAccountOpenedWith bankAccountId now 1,
            BankAccountClosedWith bankAccountId now 2
        )
        .When(
        RecordDeposit
            { Amount = amount
              CashierId = cashierId
              Now = now }
    )
        .ThenThrows<InvalidOperationException>
    |> ignore
