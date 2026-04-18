
module BookLibrary.Shared.Commons
open System
open System.Text.Json.Serialization
open Microsoft.Extensions.Configuration
open System.Threading
open System.Threading.Tasks


// Guid must be the AggregateId and int the EventId
// this conflicts with the one in the libary ouch
type AggregateViewerAsync2<'A> = Option<CancellationToken> -> Guid -> Task<Result<int * 'A,string>>     

let random = System.Random()

type ReservationCode = 
    | ReservationCode of string
    | EmptyReservationCode
    with
        static member New() = ReservationCode(random.Next(100000, 999999).ToString())
        member this.Value = 
            match this with
            | ReservationCode v -> v
            | EmptyReservationCode -> ""


type BookId =
    | BookId of Guid
    with
        static member New() = BookId(Guid.NewGuid())
        member this.Value = 
            match this with
            | BookId v -> v

type ReviewId =
    | ReviewId of Guid
    with
        static member New() = ReviewId(Guid.NewGuid())
        member this.Value = 
            match this with
            | ReviewId v -> v

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

type ShortLang =
    | ShortLocale of string
    with 
        static member 
            New(locale: string) =
                if (locale.Length < 2) then
                    (ShortLocale "en")
                else
                    match locale.Substring(0, 2).ToLower() with
                    | "en" -> (ShortLocale "en")
                    | "it" -> (ShortLocale "it")
                    | _ -> (ShortLocale "en")
            member this.Value = 
                match this with
                | ShortLocale v -> v

type Year = 
    | Year of int
    with
        static member New(year: int) = Year(year)
        member this.Value = 
            match this with
            | Year v -> v

type PhoneNumber =
    | PhoneNumber of string
    | InvalidPhoneNumber of string
    | EmptyPhoneNumber
    with
        static member IsValid (phoneNumber: string) = 
            if String.IsNullOrWhiteSpace(phoneNumber) then false
            else
                let cleanPhoneNumber = phoneNumber.Trim().Replace(" ", "")
                let regex = System.Text.RegularExpressions.Regex(@"^(\+|00)?[0-9]{7,15}$")
                regex.IsMatch(cleanPhoneNumber)
        static member New (phoneNumber: string) = 
            if PhoneNumber.IsValid(phoneNumber) then Ok (PhoneNumber phoneNumber)
            else if String.IsNullOrWhiteSpace(phoneNumber) then Error "Empty phone number"
            else Error "Invalid phone number"
        member this.Value = 
            match this with
            | PhoneNumber v -> v
            | InvalidPhoneNumber v -> v
            | EmptyPhoneNumber -> ""
        member this.IsNone = 
            match this with
            | EmptyPhoneNumber -> true
            | _ -> false
        member this.IsInvalid = 
            match this with
            | InvalidPhoneNumber _ -> true
            | _ -> false

type ApprovalStatus =
    | Pending
    | Approved of DateTime
    | Rejected of DateTime
    with
        member this.Value = 
            match this with
            | Pending -> "Pending"
            | Approved _ -> "Approved"
            | Rejected _ -> "Rejected"

type FiscalCode = 
    | FiscalCode of string
    | InvalidFiscalCode of string
    | EmptyFiscalCode
    with
        static member IsValid (fiscalCode: string) = 
            if String.IsNullOrWhiteSpace(fiscalCode) then false
            else
                let cf = fiscalCode.Trim().ToUpper()
                if cf.Length <> 16 then false
                else
                    let regex = System.Text.RegularExpressions.Regex("^[A-Z]{6}[0-9LMNPQRSTUV]{2}[ABCDEHLMPRST]{1}[0-9LMNPQRSTUV]{2}[A-Z]{1}[0-9LMNPQRSTUV]{3}[A-Z]{1}$")
                    if not (regex.IsMatch(cf)) then false
                    else
                        let oddValues = [| 1; 0; 5; 7; 9; 13; 15; 17; 19; 21; 1; 0; 5; 7; 9; 13; 15; 17; 19; 21; 2; 4; 18; 20; 11; 3; 6; 8; 12; 14; 16; 10; 22; 25; 24; 23 |]
                        let evenValues = [| 0; 1; 2; 3; 4; 5; 6; 7; 8; 9; 0; 1; 2; 3; 4; 5; 6; 7; 8; 9; 10; 11; 12; 13; 14; 15; 16; 17; 18; 19; 20; 21; 22; 23; 24; 25 |]
                        
                        let getValue (c: char) (isOdd: bool) =
                            let index = 
                                if Char.IsDigit c then int c - int '0'
                                else int c - int 'A' + 10
                            if isOdd then oddValues.[index] else evenValues.[index]

                        let sum = 
                            cf 
                            |> Seq.take 15 
                            |> Seq.mapi (fun i c -> getValue c (i % 2 = 0)) 
                            |> Seq.sum
                        
                        let expectedCheckChar = char (int 'A' + (sum % 26))
                        cf.[15] = expectedCheckChar
        static member New (fiscalCode: string) = 
            if FiscalCode.IsValid(fiscalCode) then Ok (FiscalCode fiscalCode)
            else Error "Invalid Fiscal Code"
        static member NewInvalid (fiscalCode: string) = 
            InvalidFiscalCode fiscalCode
        static member NewEmpty () = 
            EmptyFiscalCode
        member this.Value = 
            match this with
            | FiscalCode v -> v
            | InvalidFiscalCode v -> v
            | EmptyFiscalCode -> ""
        member this.IsNone = 
            match this with
            | EmptyFiscalCode -> true
            | _ -> false
    
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

        member this.IsValidIsbn = 
            match this with
            | Isbn _ -> true
            | _ -> false

        member this.IsNoneIsbn = 
            match this with
            | EmptyIsbn -> true
            | _ -> false

type ThumbRoughSize =
    | Small
    | Medium
    | Large
    with
        member this.ShortPrint = 
            match this with
            | Small -> "S"
            | Medium -> "M"
            | Large -> "L"
    

type AuthorId =
    | AuthorId of Guid
    with
        static member New() = AuthorId(Guid.NewGuid())
        member this.Value = 
            match this with
            | AuthorId v -> v

type MailQueueItemId =
    | MailQueueItemId of Guid
    with
        static member New() = MailQueueItemId(Guid.NewGuid())
        member this.Value = 
            match this with
            | MailQueueItemId v -> v

type MailQueueId =
    | MailQueueId of Guid
    with
        static member New() = MailQueueId(Guid.NewGuid())
        member this.Value = 
            match this with
            | MailQueueId v -> v

type EmailFrom = 
    | EmailFrom of string
    with
        static member New(email: string) = EmailFrom(email)
        member this.Value = 
            match this with
            | EmailFrom v -> v

type NameFrom = 
    | NameFrom of string
    with
        static member New(name: string) = NameFrom(name)
        member this.Value = 
            match this with
            | NameFrom v -> v

type EmailTo = 
    | EmailTo of string
    with
        static member New(email: string) = EmailTo(email)
        member this.Value = 
            match this with
            | EmailTo v -> v

type Subject = 
    | Subject of string
    with
        static member New(subject: string) = Subject(subject)
        member this.Value = 
            match this with
            | Subject v -> v

type Body = 
    | Body of string
    with
        static member New(body: string) = Body(body)
        member this.Value = 
            match this with
            | Body v -> v

type IsbnRegistryId =
    | IsbnRegistryId of Guid
    with
        static member New() = IsbnRegistryId(Guid.NewGuid())
        member this.Value = 
            match this with
            | IsbnRegistryId v -> v

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
                this.Sealed //  || (dateNow.ToUniversalTime() - this.DateTime.ToUniversalTime()).TotalMinutes > (float sealTimeoutInMinutes)
        member this.Unseal(dateTime: DateTime) =
            { this with DateTime = dateTime.ToUniversalTime(); Sealed = false } 
        member this.Seal(dateTime: DateTime) =
            { this with DateTime = dateTime.ToUniversalTime(); Sealed = true }

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
        member this.Shift (dateTime: DateTime) = 
            { this with Start = dateTime; End = dateTime + (this.End - this.Start) }
let jsonOptions =
    JsonFSharpOptions.Default()
        .ToJsonSerializerOptions()

type Locale = 
    | Locale of string
    with 
        static member New (locale: string) = 
            match locale.ToLower() with
            | "en-us" | "en-gb" | "en" -> Ok (Locale "en")
            | "it-it" | "it" -> Ok (Locale "it")
            | _ -> Error $"Locale {locale} is not supported"
        member this.Value = 
            match this with
            | Locale v -> v

type Availability =
    | Circulating
    | ReferenceOnly
    | Unspecified
    with
        override this.ToString () = 
            match this with
            | Circulating -> "Circulating"
            | ReferenceOnly -> "ReferenceOnly"
            | Unspecified -> "Unspecified"
        static member FromString (s: string) = 
            match s.ToLower() with
            | "circulating" -> Circulating
            | "referenceonly" -> ReferenceOnly
            | _ -> Unspecified
        static member AllCases () = 
            [ Circulating; ReferenceOnly; Unspecified ]

type LoanStatus =
    | InProgress
    | Returned of DateTime

type ReservationStatus =
    | Pending
    | Loaned

type AvailabilityStatus =
    | Available
    | NotAvailable
    | Reserved
    | Unspecified
    | Consultable

type YearSearch =
    | Before of int
    | After of int
    | Exact of int
    | Range of int * int
    with
        static member New (year: int) = 
            Exact year

type Category = 
    | Fiction
    | NonFiction
    | ScienceFiction
    | Fantasy
    | Mystery
    | Thriller
    | Romance
    | HistoricalFiction
    | Biography
    | Autobiography
    | Memoir
    | Poetry
    | Drama
    | Science
    | Technology
    | History
    | Geography
    | Philosophy
    | Psychology
    | Sociology
    | PoliticalScience
    | Economics
    | Business
    | Law
    | Education
    | Health
    | Sports
    | Travel
    | Cooking
    | Art
    | Music
    | Film
    | Photography
    | Fashion
    | Gaming
    | Other
    with
        static member New (category: string) = 
            match category.ToLower() with
            | "fiction" -> Fiction
            | "nonfiction" -> NonFiction
            | "sciencefiction" -> ScienceFiction
            | "fantasy" -> Fantasy
            | "mystery" -> Mystery
            | "thriller" -> Thriller
            | "romance" -> Romance
            | "historicalfiction" -> HistoricalFiction
            | "biography" -> Biography
            | "autobiography" -> Autobiography
            | "memoir" -> Memoir
            | "poetry" -> Poetry
            | "drama" -> Drama
            | "science" -> Science
            | "technology" -> Technology
            | "history" -> History
            | "geography" -> Geography
            | "philosophy" -> Philosophy
            | "psychology" -> Psychology
            | "sociology" -> Sociology
            | "politicalscience" -> PoliticalScience
            | "economics" -> Economics
            | "business" -> Business
            | "law" -> Law
            | "education" -> Education
            | "health" -> Health
            | "sports" -> Sports
            | "travel" -> Travel
            | "cooking" -> Cooking
            | "art" -> Art
            | "music" -> Music
            | "film" -> Film
            | "photography" -> Photography
            | "fashion" -> Fashion
            | "gaming" -> Gaming
            | _ -> Other

        static member New (locale: Locale, category: string) = 
            match locale.Value with
            | "en" -> 
                Category.New category
            | "it" -> 
                match category.ToLower() with
                | "narrativa" -> Fiction
                | "saggistica" -> NonFiction
                | "fantascienza" -> ScienceFiction
                | "fantasy" -> Fantasy
                | "giallo" -> Mystery
                | "thriller" -> Thriller
                | "rosa" -> Romance
                | "narrativa storica" -> HistoricalFiction
                | "biografia" -> Biography
                | "autobiografia" -> Autobiography
                | "memorie" -> Memoir
                | "poesia" -> Poetry
                | "dramma" -> Drama
                | "scienza" -> Science
                | "tecnologia" -> Technology
                | "storia" -> History
                | "geografia" -> Geography
                | "filosofia" -> Philosophy
                | "psicologia" -> Psychology
                | "sociologia" -> Sociology
                | "scienze politiche" -> PoliticalScience
                | "economia" -> Economics
                | "affari" -> Business
                | "legge" -> Law
                | "educazione" -> Education
                | "salute" -> Health
                | "sport" -> Sports
                | "viaggi" -> Travel
                | "cucina" -> Cooking
                | "arte" -> Art
                | "musica" -> Music
                | "cinema" -> Film
                | "fotografia" -> Photography
                | "moda" -> Fashion
                | "giochi" -> Gaming
                | _ -> Other
            | _ -> failwith $"Locale {locale.Value} is not supported"

        member this.Value () = 
            match this with
            | Fiction -> "Fiction"
            | NonFiction -> "NonFiction"
            | ScienceFiction -> "ScienceFiction"
            | Fantasy -> "Fantasy"
            | Mystery -> "Mystery"
            | Thriller -> "Thriller"
            | Romance -> "Romance"
            | HistoricalFiction -> "HistoricalFiction"
            | Biography -> "Biography"
            | Autobiography -> "Autobiography"
            | Memoir -> "Memoir"
            | Poetry -> "Poetry"
            | Drama -> "Drama"
            | Science -> "Science"
            | Technology -> "Technology"
            | History -> "History"
            | Geography -> "Geography"
            | Philosophy -> "Philosophy"
            | Psychology -> "Psychology"
            | Sociology -> "Sociology"
            | PoliticalScience -> "PoliticalScience"
            | Economics -> "Economics"
            | Business -> "Business"
            | Law -> "Law"
            | Education -> "Education"
            | Health -> "Health"
            | Sports -> "Sports"
            | Travel -> "Travel"
            | Cooking -> "Cooking"
            | Art -> "Art"
            | Music -> "Music"
            | Film -> "Film"
            | Photography -> "Photography"
            | Fashion -> "Fashion"
            | Gaming -> "Gaming"
            | Other -> "Other"
        member this.Value (locale: string) = 
            match locale.ToLower() with
            | "en-us" | "en-gb" | "en" -> this.Value ()
            | "it-it" | "it" -> 
                match this with
                | Fiction -> "Narrativa"
                | NonFiction -> "Saggistica"
                | ScienceFiction -> "Fantascienza"
                | Fantasy -> "Fantasy"
                | Mystery -> "Giallo"
                | Thriller -> "Thriller"
                | Romance -> "Rosa"
                | HistoricalFiction -> "Narrativa Storica"
                | Biography -> "Biografia"
                | Autobiography -> "Autobiografia"
                | Memoir -> "Memorie"
                | Poetry -> "Poesia"
                | Drama -> "Dramma"
                | Science -> "Scienza"
                | Technology -> "Tecnologia"
                | History -> "Storia"
                | Geography -> "Geografia"
                | Philosophy -> "Filosofia"
                | Psychology -> "Psicologia"
                | Sociology -> "Sociologia"
                | PoliticalScience -> "Scienze Politiche"
                | Economics -> "Economia"
                | Business -> "Affari"
                | Law -> "Legge"
                | Education -> "Educazione"
                | Health -> "Salute"
                | Sports -> "Sport"
                | Travel -> "Viaggi"
                | Cooking -> "Cucina"
                | Art -> "Arte"
                | Music -> "Musica"
                | Film -> "Cinema"
                | Photography -> "Fotografia"
                | Fashion -> "Moda"
                | Gaming -> "Giochi"
                | Other -> "Altro"
            | _ -> failwith $"Locale {locale} is not supported"

        member this.Value (locale: Locale) = 
            this.Value (locale.Value)

        static member FromString (category: string) = 
            match category.ToLower() with
            | "fiction" -> Fiction
            | "nonfiction" -> NonFiction
            | "sciencefiction" -> ScienceFiction
            | "fantasy" -> Fantasy
            | "mystery" -> Mystery
            | "thriller" -> Thriller
            | "romance" -> Romance
            | "historicalfiction" -> HistoricalFiction
            | "biography" -> Biography
            | "autobiography" -> Autobiography
            | "memoir" -> Memoir
            | "poetry" -> Poetry
            | "drama" -> Drama
            | "science" -> Science
            | "technology" -> Technology
            | "history" -> History
            | "geography" -> Geography
            | "philosophy" -> Philosophy
            | "psychology" -> Psychology
            | "sociology" -> Sociology
            | "politicalscience" -> PoliticalScience
            | "economics" -> Economics
            | "business" -> Business
            | "law" -> Law
            | "education" -> Education
            | "health" -> Health
            | "sports" -> Sports
            | "travel" -> Travel
            | "cooking" -> Cooking
            | "art" -> Art
            | "music" -> Music
            | "film" -> Film
            | "photography" -> Photography
            | "fashion" -> Fashion
            | "gaming" -> Gaming
            | _ -> Other

        static member AllCases () = 
            [ Fiction; 
            NonFiction; 
            ScienceFiction; 
            Fantasy; 
            Mystery; 
            Thriller; 
            Romance; 
            HistoricalFiction; 
            Biography; 
            Autobiography; 
            Memoir; 
            Poetry; 
            Drama; 
            Science; 
            Technology; 
            History; 
            Geography; 
            Philosophy; 
            Psychology; 
            Sociology; 
            PoliticalScience; 
            Economics; 
            Business; 
            Law; 
            Education; 
            Health; 
            Sports; 
            Travel; 
            Cooking; 
            Art; 
            Music; 
            Film; 
            Photography; 
            Fashion; 
            Gaming; 
            Other ]

