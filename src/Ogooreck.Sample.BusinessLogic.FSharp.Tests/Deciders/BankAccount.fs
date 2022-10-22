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

    static member Create(opened: BankAccountOpened) =
        Open
            {| Id = opened.BankAccountId
               Balance = 0M
               Version = 1 |}

    member this.Apply(depositRecorded: DepositRecorded) =
        match this with
        | Open openAccount ->
            {| openAccount with
                Balance = openAccount.Balance + depositRecorded.Amount
                Version = depositRecorded.Version |}
            |> BankAccount.Open
        | NotInitialised _ -> this
        | Closed _ -> this

    member this.Apply(depositRecorded: CashWithdrawnFromATM) =
        match this with
        | Open openAccount ->
            {| openAccount with
                Balance = openAccount.Balance - depositRecorded.Amount
                Version = depositRecorded.Version |}
            |> BankAccount.Open
        | NotInitialised _ -> this
        | Closed _ -> this

    member this.Apply(bankAccountClosed: BankAccountClosed) =
        match this with
        | Open openAccount ->
            {| Id = openAccount.Id
               Version = bankAccountClosed.Version |}
            |> BankAccount.Closed
        | NotInitialised _ -> this
        | Closed _ -> this


let evolve (state: BankAccount) (event: Event) : BankAccount =
    match event with
    | BankAccountOpened e -> BankAccount.Create(e)
    | DepositRecorded e -> state.Apply(e)
    | CashWithdrawnFromATM e -> state.Apply(e)
    | BankAccountClosed e -> state.Apply(e)
