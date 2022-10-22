module Deciders.BankAccount

open System

type BankAccountOpened =
    { BankAccountId: Guid
      AccountNumber: string
      ClientId: Guid
      CurrencyISOCode: string
      CreatedAt: DateTimeOffset
      Version: int64 }

type DepositRecorded =
    { BankAccountId: Guid
      Amount: decimal
      CashierId: Guid
      RecordedAt: DateTimeOffset
      Version: int64 }

type CashWithdrawnFromATM =
    { BankAccountId: Guid
      Amount: decimal
      ATMId: Guid
      RecordedAt: DateTimeOffset
      Version: int64 }

type BankAccountClosed =
    { BankAccountId: Guid
      Reason: string
      ClosedAt: DateTimeOffset
      Version: int64 }

type Event =
    | BankAccountOpened of BankAccountOpened
    | DepositRecorded of DepositRecorded
    | CashWithdrawnFromATM of CashWithdrawnFromATM
    | BankAccountClosed of BankAccountClosed

type BankAccount =
    | NotInitialised
    | Open of
        {| Id: Guid
           Balance: decimal
           Version: int64 |}
    | Closed of {| Id: Guid; Version: int64 |}

let evolve (state: BankAccount) (event: Event) : BankAccount =
    match (state, event) with
    | NotInitialised _, BankAccountOpened opened ->
        {| Id = opened.BankAccountId
           Balance = 0M
           Version = 1L |}
        |> Open
    | Open openAccount, DepositRecorded depositRecorded ->
        {| openAccount with
            Balance = openAccount.Balance + depositRecorded.Amount
            Version = depositRecorded.Version |}
        |> Open
    | Open openAccount, CashWithdrawnFromATM cashWithdrawnFromAtm ->
        {| openAccount with
            Balance = openAccount.Balance - cashWithdrawnFromAtm.Amount
            Version = cashWithdrawnFromAtm.Version |}
        |> BankAccount.Open
    | Open openAccount, BankAccountClosed bankAccountClosed ->
        {| Id = openAccount.Id
           Version = bankAccountClosed.Version |}
        |> Closed
    | _ -> state
