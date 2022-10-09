module Deciders.BankAccount

open System

type BankAccountOpened =
    {| BankAccountId: Guid
       AccountNumber: string
       ClientId: Guid
       CurrencyISOCode: string
       CreatedAt: DateTimeOffset
       Version: int64 |}

type DepositRecorded =
    {| BankAccountId: Guid
       Amount: decimal
       CashierId: Guid
       RecordedAt: DateTimeOffset
       Version: int64 |}

type CashWithdrawnFromATM =
    {| BankAccountId: Guid
       Amount: decimal
       ATMId: Guid
       RecordedAt: DateTimeOffset
       Version: int64 |}

type BankAccountClosed =
    {| BankAccountId: Guid
       Reason: string
       ClosedAt: DateTimeOffset
       Version: int64 |}

type BankAccountEvent =
    | BankAccountOpened of BankAccountOpened
    | DepositRecorded of DepositRecorded
    | CashWithdrawnFromATM of CashWithdrawnFromATM
    | BankAccountClosed of BankAccountClosed

type BankAccountStatus =
    | Opened
    | Closed

type BankAccount =
    { Id: Guid
      Status: BankAccountStatus
      Balance: decimal
      Version: int64 }

    static member Create(opened: BankAccountOpened) =
        { Id = opened.BankAccountId
          Status = Opened
          Balance = 0M
          Version = 0 }

    member this.Apply(depositRecorded: DepositRecorded) =
        { this with
            Balance = this.Balance + depositRecorded.Amount
            Version = depositRecorded.Version }

    member this.Apply(depositRecorded: CashWithdrawnFromATM) =
        { this with
            Balance = this.Balance - depositRecorded.Amount
            Version = depositRecorded.Version }

    member this.Apply(depositRecorded: BankAccountClosed) =
        { this with
            Status = BankAccountStatus.Closed
            Version = depositRecorded.Version }


let evolve (state: BankAccount, event: BankAccountEvent) =
    match event with
    | BankAccountOpened e -> BankAccount.Create(e)
    | DepositRecorded e -> state.Apply(e)
    | CashWithdrawnFromATM e -> state.Apply(e)
    | BankAccountClosed e -> state.Apply(e)
