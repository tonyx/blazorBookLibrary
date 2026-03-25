
module BookLibrary.Shared.Commons
open System
open System.Text.Json.Serialization
open Microsoft.Extensions.Configuration
open System.Threading
open System.Threading.Tasks


// Guid must be the AggregateId and int the EventId
// this conflicts with the one in the libary ouch
type AggregateViewerAsync2<'A> = Option<CancellationToken> -> Guid -> Task<Result<int * 'A,string>>     

let sealTimeoutInMinutes =
    let config = 
        ConfigurationBuilder()
            .AddJsonFile("appSettings.json")
            .Build()
    config.["SealTimeoutInMinutes"] |> int

let timeSlotDurationInDays =
    let config = 
        ConfigurationBuilder()
            .AddJsonFile("appSettings.json")
            .Build()
    config.["TimeSlotLoanDurationInDays"] |> int

type BookId =
    | BookId of Guid
    with
        static member New() = BookId(Guid.NewGuid())
        member this.Value = 
            match this with
            | BookId v -> v

type ReservationId =
    | ReservationId of Guid
    with
        static member New() = ReservationId(Guid.NewGuid())
        member this.Value = 
            match this with
            | ReservationId v -> v

type LoanId =
    | LoanId of Guid
    with
        static member New() = LoanId(Guid.NewGuid())
        member this.Value = 
            match this with
            | LoanId v -> v


type AuthorName = 
    | AuthorName of string
    with
        static member New(name: string) = AuthorName(name)
        member this.Value = 
            match this with
            | AuthorName v -> v

type Title = 
    | Title of string
    with
        static member New(title: string) = Title(title)
        member this.Value = 
            match this with
            | Title v -> v

type Year = 
    | Year of int
    with
        static member New(year: int) = Year(year)
        member this.Value = 
            match this with
            | Year v -> v

type Isbn = 
    | Isbn of string
    | InvalidIsbn of string
    | EmptyIsbn
    with
        static member IsValid (isbn: string) =
            if String.IsNullOrWhiteSpace(isbn) then false
            else
                let cleanIsbn = isbn.Replace("-", "").Replace(" ", "")
                if cleanIsbn.Length = 10 then
                    let isValidFormat = 
                        cleanIsbn |> Seq.mapi (fun i c -> 
                            if i < 9 then Char.IsDigit c
                            else Char.IsDigit c || c = 'X' || c = 'x'
                        ) |> Seq.forall id
                    
                    if not isValidFormat then false
                    else
                        let sum = 
                            cleanIsbn 
                            |> Seq.mapi (fun i c -> 
                                let v = if c = 'X' || c = 'x' then 10 else int c - int '0'
                                v * (10 - i))
                            |> Seq.sum
                        sum % 11 = 0
                elif cleanIsbn.Length = 13 then
                    let isValidFormat = cleanIsbn |> Seq.forall Char.IsDigit
                    if not isValidFormat then false
                    else
                        let sum = 
                            cleanIsbn 
                            |> Seq.mapi (fun i c -> 
                                let v = int c - int '0'
                                v * (if i % 2 = 0 then 1 else 3))
                            |> Seq.sum
                        sum % 10 = 0
                else
                    false

        static member New (isbn: string) =
            if Isbn.IsValid(isbn) then Ok (Isbn isbn)
            else Error "Invalid ISBN"
        static member NewInvalid (isbn: string) =
            InvalidIsbn isbn
        static member NewEmpty () =
            EmptyIsbn

        member this.Value = 
            match this with
            | Isbn v -> v
            | InvalidIsbn v -> v
            | EmptyIsbn -> ""

type AuthorId =
    | AuthorId of Guid
    with
        static member New() = AuthorId(Guid.NewGuid())
        member this.Value = 
            match this with
            | AuthorId v -> v

type EditorId =
    | EditorId of Guid
    with
        static member New() = EditorId(Guid.NewGuid())
        member this.Value = 
            match this with
            | EditorId v -> v

type UserId =
    | UserId of Guid
    with
        static member New() = UserId(Guid.NewGuid())
        member this.Value = 
            match this with
            | UserId v -> v

type Name =
    | Name of string
    | EmptyName
    with
        static member New (name: string) = 
            if String.IsNullOrWhiteSpace(name) then 
                EmptyName
            else
                Name(name)
        member this.Value = 
            match this with
            | Name v -> v
            | EmptyName -> ""

type Isni = 
    | Isni of string
    | InvalidIsni of string
    | EmptyIsni
    with
        static member IsValid (isni: string) =
            if String.IsNullOrWhiteSpace(isni) then false
            else
                let cleanIsni = isni.Replace("-", "").Replace(" ", "")
                if cleanIsni.Length = 16 then
                    let isValidFormat = 
                        cleanIsni |> Seq.mapi (fun i c -> 
                            if i < 15 then Char.IsDigit c
                            else Char.IsDigit c || c = 'X' || c = 'x'
                        ) |> Seq.forall id
                    
                    if not isValidFormat then false
                    else
                        let sum = 
                            cleanIsni 
                            |> Seq.fold (fun acc c -> 
                                let v = if c = 'X' || c = 'x' then 10 else int c - int '0'
                                (2 * acc + v) % 11) 0
                        sum = 1
                else
                    false

        static member New (isni: string) =
            if Isni.IsValid(isni) then Ok (Isni isni)
            else Error "Invalid Isni"
        
        static member NewInvalid (isni: string) =
            InvalidIsni isni
            
        static member NewEmpty () =
            EmptyIsni

        member this.Value = 
            match this with
            | Isni v -> v
            | InvalidIsni v -> v
            | EmptyIsni -> ""

type Sealed =
    {
        DateTime : DateTime
        Sealed : bool
    }
    with
        static member New (dateTime: DateTime) = 
            {
                DateTime = dateTime
                Sealed = false
            }
        member 
            this.IsSealed (dateNow: DateTime) = 
                this.Sealed  || (dateNow - this.DateTime).TotalMinutes < sealTimeoutInMinutes
        member this.Unseal(dateTime: DateTime) =
            { this with DateTime = dateTime; Sealed = false } 
        member this.Seal(dateTime: DateTime) =
            { this with DateTime = dateTime; Sealed = true }

type Cancellation =
    {
        DateTime: DateTime
        Reason: string
    }
    with
        static member New (dateTime: DateTime) (reason: string) = 
            { DateTime = dateTime; Reason = reason }

type TimeSlot =
    {
        Start: DateTime
        End: DateTime
    }
    with
        static member New (start: DateTime) (endTime: DateTime) = 
            { Start = start; End = endTime }
        member this.IsFutureOf (dateNow: DateTime) = 
            this.Start > dateNow
        member this.Overlaps (other: TimeSlot) = 
            this.Start < other.End && other.Start < this.End
let jsonOptions =
    JsonFSharpOptions.Default()
        .ToJsonSerializerOptions()
