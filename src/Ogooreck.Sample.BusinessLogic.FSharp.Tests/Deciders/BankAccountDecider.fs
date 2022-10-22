module Deciders.BankAccountDecider

open System
open BankAccount
open BankAccountPrimitives

type OpenBankAccount =
    { BankAccountId: AccountId
      AccountNumber: AccountNumber
      ClientId: ClientId
      CurrencyISOCode: CurrencyCode
      Now: DateTimeOffset }

type RecordDeposit = { Amount: decimal; CashierId: CashierId; Now: DateTimeOffset }
type WithdrawCashFromATM = { Amount: decimal; AtmId: AtmId; Now: DateTimeOffset }
type CloseBankAccount = { Reason: string; Now: DateTimeOffset }

type Command =
    | OpenBankAccount of OpenBankAccount
    | RecordDeposit of RecordDeposit
    | WithdrawCashFromATM of WithdrawCashFromATM
    | CloseBankAccount of CloseBankAccount

let openBankAccount (command: OpenBankAccount) (bankAccount: BankAccount) =
    match bankAccount with
    | Open _ -> invalidOp "Account is already opened!"
    | Closed _ -> invalidOp "Account is already closed!"
    | NotInitialised _ ->
        BankAccountOpened
            { BankAccountId = command.BankAccountId
              AccountNumber = command.AccountNumber
              ClientId = command.ClientId
              CurrencyISOCode = command.CurrencyISOCode
              CreatedAt = command.Now
              Version = 1 }

let recordDeposit (command: RecordDeposit) (bankAccount: BankAccount) =
    match bankAccount with
    | NotInitialised _ -> invalidOp "Account is not opened!"
    | Closed _ -> invalidOp "Account is closed!"
    | Open openBankAccount ->
        DepositRecorded
            { BankAccountId = openBankAccount.Id
              Amount = command.Amount
              CashierId = command.CashierId
              RecordedAt  = command.Now
              Version = openBankAccount.Version + 1L }

let withdrawCashFromATM (command: WithdrawCashFromATM) (bankAccount: BankAccount) =
    match bankAccount with
    | NotInitialised _ -> invalidOp "Account is not opened!"
    | Closed _ -> invalidOp "Account is closed!"
    | Open openBankAccount ->
        if (openBankAccount.Balance < command.Amount) then
            invalidOp "Not enough money!"

        CashWithdrawnFromATM
            { BankAccountId = openBankAccount.Id
              Amount = command.Amount
              ATMId = command.AtmId
              RecordedAt = command.Now
              Version = openBankAccount.Version + 1L }

let closeBankAccount (command: CloseBankAccount) (bankAccount: BankAccount) =
    match bankAccount with
    | NotInitialised _ -> invalidOp "Account is not opened!"
    | Closed _ -> invalidOp "Account is already closed!"
    | Open openBankAccount ->
        BankAccountClosed
            { BankAccountId = openBankAccount.Id
              Reason = command.Reason
              ClosedAt = command.Now
              Version = openBankAccount.Version + 1L }

let decide command bankAccount =
    match command with
    | OpenBankAccount c -> openBankAccount c bankAccount
    | RecordDeposit c -> recordDeposit c bankAccount
    | WithdrawCashFromATM c -> withdrawCashFromATM c bankAccount
    | CloseBankAccount c -> closeBankAccount c bankAccount
