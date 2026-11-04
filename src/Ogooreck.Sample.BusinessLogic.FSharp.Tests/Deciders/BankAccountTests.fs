// begin-snippet: FSharpBankAccountDeciderTests
module BankAccountTests

open System
open Deciders.BankAccount
open Deciders.BankAccountPrimitives
open Deciders.BankAccountDecider
open Ogooreck.BusinessLogic
open FsCheck.Xunit

let random = Random()

let spec =
    Specification.For(decide, evolve, Initial)

let BankAccountOpenedWith bankAccountId now version =
    let accountNumber =
        AccountNumber.parse (Guid.NewGuid().ToString())

    let clientId = ClientId.newId ()

    let currencyISOCode =
        CurrencyIsoCode.parse "USD"

    BankAccountOpened
        { BankAccountId = bankAccountId
          AccountNumber = accountNumber
          ClientId = clientId
          CurrencyIsoCode = currencyISOCode
          CreatedAt = now
          Version = version }

let BankAccountClosedWith bankAccountId now version =
    BankAccountClosed
        { BankAccountId = bankAccountId
          Reason = Guid.NewGuid().ToString()
          ClosedAt = now
          Version = version }

[<Property>]
let ``GIVEN missing account WHEN open THEN account is opened``
    bankAccountId
    accountNumber
    clientId
    currencyISOCode
    now
    =
    let notExistingAccount = Array.empty

    spec
        .Given(notExistingAccount)
        .When(
            OpenBankAccount
                { BankAccountId = bankAccountId
                  AccountNumber = accountNumber
                  ClientId = clientId
                  CurrencyIsoCode = currencyISOCode
                  Now = now }
        )
        .Then(
            BankAccountOpened
                { BankAccountId = bankAccountId
                  AccountNumber = accountNumber
                  ClientId = clientId
                  CurrencyIsoCode = currencyISOCode
                  CreatedAt = now
                  Version = 1 }
        )
    |> ignore

[<Property>]
let ``GIVEN open account WHEN deposit THEN deposit is recorded``
    bankAccountId
    amount
    cashierId
    now
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
let ``GIVEN closed account WHEN deposit THEN throws``
    bankAccountId
    amount
    cashierId
    now
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
        .ThenThrows<InvalidOperationException>()
    |> ignore
// end-snippet
