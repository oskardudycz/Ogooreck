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

let evolve (state: BankAccount) (event: Event) : BankAccount =
    match state, event with
    | Initial _, BankAccountOpened opened ->
        Open
            {| Id = opened.BankAccountId
               Balance = 0M
               Version = 1L |}
    | Open openAccount, DepositRecorded depositRecorded ->
        Open
            {| openAccount with
                Balance = openAccount.Balance + depositRecorded.Amount
                Version = depositRecorded.Version |}
    | Open openAccount, CashWithdrawnFromAtm cashWithdrawnFromAtm ->
        Open
            {| openAccount with
                Balance = openAccount.Balance - cashWithdrawnFromAtm.Amount
                Version = cashWithdrawnFromAtm.Version |}
    | Open openAccount, BankAccountClosed bankAccountClosed ->
        Closed
            {| Id = openAccount.Id
               Version = bankAccountClosed.Version |}
    | _ -> state
