
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
            .AddJsonFile("appsettings.json", true)
            .Build()
    let v = config.["SealTimeoutInMinutes"]
    if String.IsNullOrWhiteSpace v then 60 else int v


let timeSlotDurationInDays =
    let config = 
        ConfigurationBuilder()
            .AddJsonFile("appsettings.json", true)
            .Build()
    let v = config.["BookLibrary::TimeSlotLoanDurationInDays"]
    if String.IsNullOrWhiteSpace v then 30 else int v


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
                this.Sealed  || (dateNow.ToUniversalTime() - this.DateTime.ToUniversalTime()).TotalMinutes > (float sealTimeoutInMinutes)
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

