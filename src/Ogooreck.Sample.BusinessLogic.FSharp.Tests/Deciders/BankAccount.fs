module Deciders.BankAccount

open System
open BankAccountPrimitives

type BankAccountOpened =
    { BankAccountId: AccountId
      AccountNumber: AccountNumber
      ClientId: ClientId
      CurrencyIsoCode: CurrencyIsoCode
      CreatedAt: DateTimeOffset
      Version: int64 }

type DepositRecorded =
    { BankAccountId: AccountId
      Amount: decimal
      CashierId: CashierId
      RecordedAt: DateTimeOffset
      Version: int64 }

type CashWithdrawnFromATM =
    { BankAccountId: AccountId
      Amount: decimal
      AtmId: AtmId
      RecordedAt: DateTimeOffset
      Version: int64 }

type BankAccountClosed =
    { BankAccountId: AccountId
      Reason: string
      ClosedAt: DateTimeOffset
      Version: int64 }

type Event =
    | BankAccountOpened of BankAccountOpened
    | DepositRecorded of DepositRecorded
    | CashWithdrawnFromAtm of CashWithdrawnFromATM
    | BankAccountClosed of BankAccountClosed

type BankAccount =
    | Initial
    | Open of
        {| Id: AccountId
           Balance: decimal
           Version: int64 |}
    | Closed of {| Id: AccountId; Version: int64 |}

let evolve bankAccount bankAccountEvent : BankAccount =
    match bankAccount, bankAccountEvent with
    | Initial _, BankAccountOpened event ->
        Open
            {| Id = event.BankAccountId
               Balance = 0M
               Version = 1L |}
    | Open state, DepositRecorded event ->
        Open
            {| state with
                Balance = state.Balance + event.Amount
                Version = event.Version |}
    | Open state, CashWithdrawnFromAtm event ->
        Open
            {| state with
                Balance = state.Balance - event.Amount
                Version = event.Version |}
    | Open state, BankAccountClosed bankAccountClosed ->
        Closed
            {| Id = state.Id
               Version = bankAccountClosed.Version |}
    | _ -> bankAccount
