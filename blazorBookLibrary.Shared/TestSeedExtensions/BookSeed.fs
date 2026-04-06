
namespace BookLibrary.Tests.TestSeedExtensions

open System
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open FsToolkit.ErrorHandling

module BookSeeds =
    let bookPictureUri = Uri("http://books.google.com/books/content?id=VWCEkgAACAAJ&printsec=frontcover&img=1&zoom=1&source=gbs_api")

    let rnd = Random()
    
    let nouns = [| "Shadow"; "Night"; "Sea"; "Rose"; "Memory"; "Labyrinth"; "Ghost"; "Silence"; "Storm"; "Legacy"; "Diamond"; "Wind"; "Mountain"; "Forest"; "Heart"; "Sword"; "Key"; "Mirror"; "Bridge"; "Tower"; "Symphony"; "Whisper"; "Echo"; "Dream"; "Sunlight"; "Winter"; "Summer"; "Autumn"; "Spring"; "Kingdom"; "Soul"; "Flame"; "Ocean"; "Ship"; "Island" |]
    let adjectives = [| "Golden"; "Silent"; "Lost"; "Infinite"; "Ancient"; "Bright"; "Dark"; "Shattered"; "Hidden"; "Enchanted"; "Crimson"; "Frozen"; "Distant"; "Secret"; "Holy"; "Midnight"; "Broken"; "Falling"; "Rising"; "Wild"; "Eternal"; "Fading"; "Mystic"; "Hallowed"; "Forgotten" |]
    let verbs = [| "Dancing"; "Singing"; "Forgotten"; "Broken"; "Rising"; "Seeking"; "Waiting"; "Guarding"; "Lost"; "Wandering" |]
    let prepositions = [| "of"; "in"; "by"; "under"; "beyond"; "without" |]
    
    let pick (arr: 'a[]) = arr.[rnd.Next(arr.Length)]

    let private generateTitleText () =
        let patterns = [|
            fun () -> sprintf "The %s of %s" (pick nouns) (pick nouns)
            fun () -> sprintf "%s %s" (pick adjectives) (pick nouns)
            fun () -> sprintf "The %s %s" (pick adjectives) (pick nouns)
            fun () -> sprintf "%s and %s" (pick nouns) (pick nouns)
            fun () -> sprintf "%s the %s" (pick verbs) (pick nouns)
            fun () -> sprintf "%s in the %s" (pick nouns) (pick nouns)
            fun () -> sprintf "%s %s" (pick nouns) (pick adjectives)
            fun () -> sprintf "A %s for %s" (pick nouns) (pick nouns)
            fun () -> sprintf "%s's %s" (pick nouns) (pick nouns)
            fun () -> sprintf "Beyond the %s %s" (pick adjectives) (pick nouns)
        |]
        (pick patterns) ()

    let generateRandomTitle () =
        generateTitleText() |> Title.New

    let randomCategory () =
        let categories = Category.AllCases ()
        categories |> List.item (rnd.Next(categories.Length))

    let randomIsbn () =
        let prefix = if rnd.Next(2) = 0 then "978" else "979"
        let first12 = sprintf "%s%09d" prefix (rnd.Next(1000000000))
        let sum = 
            first12 
            |> Seq.mapi (fun i c -> 
                let v = int c - int '0'
                v * (if i % 2 = 0 then 1 else 3))
            |> Seq.sum
        let checkDigit = (10 - (sum % 10)) % 10
        Isbn.New (sprintf "%s%d" first12 checkDigit)

    let randomBook (authors: List<AuthorId>) =
        result
            {
                let title = generateRandomTitle()
                let! isbn = randomIsbn()
                let year = Year.New(2022)
                let description = "This is a random book description."
                let mainCategory = randomCategory()
                let additionalCategories = [randomCategory(); randomCategory()]
                let book = Book.New title authors [] [] None mainCategory additionalCategories year isbn (bookPictureUri |> Some)
                return {book with Description = description |> Some }
            }