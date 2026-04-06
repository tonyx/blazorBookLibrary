namespace BookLibrary.Tests.TestSeedExtensions

open System
open BookLibrary.Domain
open BookLibrary.Shared.Commons

module AuthorSeeds =
    let personPictureUri = Uri("https://upload.wikimedia.org/wikipedia/commons/thumb/2/24/Stephen_King_at_the_2024_Toronto_International_Film_Festival_2_%28cropped%29.jpg/120px-Stephen_King_at_the_2024_Toronto_International_Film_Festival_2_%28cropped%29.jpg")
    let bookPictureUri = Uri("http://books.google.com/books/content?id=VWCEkgAACAAJ&printsec=frontcover&img=1&zoom=1&source=gbs_api")

    let firstNames = [|
        "Alessandro"; "Andrea"; "Antonio"; "Carlo"; "Domenico"; "Elena"; "Fabio"; "Francesco"; "Giorgio"; "Giovanni"
        "Giulia"; "Giuseppe"; "Laura"; "Luca"; "Luigi"; "Marco"; "Mario"; "Paolo"; "Pietro"; "Roberto"
        "Salvatore"; "Stefano"; "Tommaso"; "Valentino"; "Vincenzo"; "Adam"; "Alice"; "Benjamin"; "Charlotte"; "Daniel"
        "David"; "Edward"; "Elizabeth"; "Emma"; "George"; "Henry"; "Isabella"; "Jack"; "James"; "Jane"
        "John"; "Joseph"; "Lucy"; "Mary"; "Michael"; "Oliver"; "Robert"; "Sarah"; "Thomas"; "William"
    |]

    let lastNames = [|
        "Rossi"; "Ferrari"; "Esposito"; "Bianchi"; "Romano"; "Colombo"; "Ricci"; "Marino"; "Greco"; "Bruno"
        "Gallo"; "Conti"; "De Luca"; "Costa"; "Giordano"; "Mancini"; "Rizzo"; "Lombardi"; "Moretti"; "Barbieri"
        "Fontana"; "Santoro"; "Mariani"; "Ferrara"; "Galli"; "Smith"; "Johnson"; "Williams"; "Brown"; "Jones"
        "Garcia"; "Miller"; "Davis"; "Rodriguez"; "Martinez"; "Hernandez"; "Lopez"; "Gonzalez"; "Wilson"; "Anderson"
        "Thomas"; "Taylor"; "Moore"; "Jackson"; "Martin"; "Lee"; "Perez"; "Thompson"; "White"; "Harris"
    |]

    let rnd = Random()

    let generateRandomAuthor () =
        let firstName = firstNames.[rnd.Next(firstNames.Length)]
        let lastName = lastNames.[rnd.Next(lastNames.Length)]
        let fullName = sprintf "%s %s" firstName lastName
        Author.NewWithOptionalIsniAndImageUrl(Name.New fullName, imageUrl = personPictureUri)
