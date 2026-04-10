module AuthorsServiceTests

open TestSetup
open Expecto
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Services
open System.Threading

[<Tests>]
let tests =
    testList "authors service" [
        testCaseTask "create and retrieve an author" <| fun _ -> task {
            setUp()
            let authorService = getAuthorService()
            let author = Author.NewWithoutIsni (Name.New "John Doe")

            let! addAuthor = authorService.AddAuthorAsync author
            Expect.isOk addAuthor "should be ok"
            let! getAuthor = authorService.GetAuthorAsync author.AuthorId
            Expect.isOk getAuthor "should be ok"
        }

        testCaseTask "the author contains reference to the book they are author of " <| fun _ -> task {
            setUp()
            let authorService = getAuthorService()
            let bookService = getBookService()
            let author = Author.NewWithoutIsni (Name.New "John Doe")
            let! addAuthor = authorService.AddAuthorAsync author
            Expect.isOk addAuthor "should be ok"

            let book = Book.New (Title.New "The Great Gatsby") [author.AuthorId] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync book
            Expect.isOk addBook "should be ok"

            let! getAuthorResult = authorService.GetAuthorAsync author.AuthorId
            let (author: Author ) = getAuthorResult |> Result.get
            Expect.isTrue (List.contains book.BookId author.Books) "should contain the book"
        }

        testCaseTask "the author contains references to the books they are author of, test many authors - Ok " <| fun _ -> task {
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()
            let author = Author.NewWithoutIsni (Name.New "John Doe")
            let! addAuthor = authorService.AddAuthorAsync author
            Expect.isOk addAuthor "should be ok"

            let author2 = Author.NewWithoutIsni (Name.New "Murakami")
            let! addAuthor2 = authorService.AddAuthorAsync author2
            Expect.isOk addAuthor2 "should be ok"

            let book = Book.New (Title.New "The Great Gatsby") [author.AuthorId; author2.AuthorId] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync book
            Expect.isOk addBook "should be ok"
            let! retrieveAuthorResult = authorService.GetAuthorAsync author.AuthorId
            Expect.isOk retrieveAuthorResult "should be ok"

            let (authorRetrieved: Author) = retrieveAuthorResult |> Result.get
            Expect.isTrue (authorRetrieved.Books |> List.contains book.BookId) "should contain the book"

            let! retrieveAuthor2Result = authorService.GetAuthorAsync author2.AuthorId
            Expect.isOk retrieveAuthor2Result "should be ok"

            let (authorRetrieved2: Author) = retrieveAuthor2Result |> Result.get
            Expect.isTrue (authorRetrieved2.Books |> List.contains book.BookId) "should contain the book"
        }

        testCaseTask "add an author, add a book without authors and then add an author to that book and retrieve the author checking the mutal references  - Ok" <| fun _ -> task {
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()
            let author = Author.NewWithoutIsni (Name.New "John Doe")
            let! addAuthor = authorService.AddAuthorAsync author
            Expect.isOk addAuthor "should be ok"

            let book = Book.New (Title.New "The Great Gatsby") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync book
            Expect.isOk addBook "should be ok"
            
            let! _ = (bookService :> IBookService).AddAuthorToBookAsync(author.AuthorId, book.BookId)

            let! retrieveAuthorResult = authorService.GetAuthorAsync author.AuthorId
            Expect.isOk retrieveAuthorResult "should be ok"

            let (authorRetrieved: Author) = retrieveAuthorResult |> Result.get
            Expect.isTrue (authorRetrieved.Books |> List.contains book.BookId) "should contain the book"

            let! retrieveBookResult = bookService.GetBookAsync book.BookId
            Expect.isOk retrieveBookResult "should be ok"

            let (bookRetrieved: Book) = retrieveBookResult |> Result.get
            Expect.isTrue (bookRetrieved.Authors |> List.contains author.AuthorId) "should contain the author"
        }

        testCaseTask "add an author, add a book without authors and then add an author to that book and retrieve the author checking the mutal references async - Ok" <| fun _ -> task {
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()
            let author = Author.NewWithoutIsni (Name.New "John Doe")
            let! addAuthor = authorService.AddAuthorAsync author
            Expect.isOk addAuthor "should be ok"

            let book = Book.New (Title.New "The Great Gatsby") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync book
            Expect.isOk addBook "should be ok"
            
            let! _ = (bookService :> IBookService).AddAuthorToBookAsync(author.AuthorId, book.BookId)

            let! retrieveAuthorResult = authorService.GetAuthorAsync author.AuthorId
            Expect.isOk retrieveAuthorResult "should be ok"

            let (authorRetrieved: Author) = retrieveAuthorResult |> Result.get
            Expect.isTrue (authorRetrieved.Books |> List.contains book.BookId) "should contain the book"

            let! retrieveBookResult = bookService.GetBookAsync book.BookId
            Expect.isOk retrieveBookResult "should be ok"

            let (bookRetrieved: Book) = retrieveBookResult |> Result.get
            Expect.isTrue (bookRetrieved.Authors |> List.contains author.AuthorId) "should contain the author"
        }

        testCaseTask "add two authors and retrieve them all - Ok" <| fun _ -> task {
            setUp ()
            let authorService = getAuthorService()
            let author1 = Author.NewWithoutIsni (Name.New "Author One")
            let author2 = Author.NewWithoutIsni (Name.New "Author Two")
            
            let! addAuthor1 = authorService.AddAuthorAsync author1
            Expect.isOk addAuthor1 "should be ok"
            
            let! addAuthor2 = authorService.AddAuthorAsync author2
            Expect.isOk addAuthor2 "should be ok"
            
            let! getAllResult = (authorService :> IAuthorService).GetAllAsync()
            
            Expect.isOk getAllResult "should be ok"
            let allAuthors = getAllResult |> Result.get
            Expect.equal allAuthors.Length 2 "should have 2 authors"
            Expect.isTrue (allAuthors |> List.exists (fun a -> a.AuthorId = author1.AuthorId)) "should contain author 1"
            Expect.isTrue (allAuthors |> List.exists (fun a -> a.AuthorId = author2.AuthorId)) "should contain author 2"
        }

        testCaseTask "add multiple authors - Ok" <| fun _ -> task {
            setUp ()
            let authorService = getAuthorService()
            let authors = 
                [
                    Author.NewWithoutIsni (Name.New "Author A")
                    Author.NewWithoutIsni (Name.New "Author B")
                    Author.NewWithoutIsni (Name.New "Author C")
                ]
            
            let! addAuthorsResult = (authorService :> IAuthorService).AddAuthorsAsync (authors)
            Expect.isOk addAuthorsResult "should be ok"
            
            let! getAllResult = (authorService :> IAuthorService).GetAllAsync()
            
            Expect.isOk getAllResult "should be ok"
            let all = getAllResult |> Result.get
            Expect.equal all.Length 3 "should have 3 authors"
        }

        testCaseTask "filtering authors by name - Ok" <| fun _ -> task {
            setUp ()
            let authorService = getAuthorService()
            let name1 = Name.New "John Smith"
            let name2 = Name.New "Jane Doe"
            let author1 = Author.NewWithoutIsni name1
            let author2 = Author.NewWithoutIsni name2
            
            let! _ = (authorService :> IAuthorService).AddAuthorsAsync [author1; author2]
            
            let! filteredResult = (authorService :> IAuthorService).SearchByNameAsync name1
            let filtered = filteredResult |> Result.get

            Expect.equal filtered.Length 1 "should have 1 author"
            Expect.equal filtered.[0].AuthorId author1.AuthorId "should be author 1"
        }

        testCaseTask "filtering authors by isni - Ok" <| fun _ -> task {
            setUp ()
            let authorService = getAuthorService()
            let isni1 = Isni.New "0000 0001 2103 2683" |> Result.get
            let isni2 = Isni.New "0000 0001 2103 2691" |> Result.get
            let author1 = Author.New (Name.New "Austen") isni1
            let author2 = Author.New (Name.New "Shakespeare") isni2
            
            let! _ = (authorService :> IAuthorService).AddAuthorsAsync [author1; author2]
            
            let! filteredResult = (authorService :> IAuthorService).SearchByIsniAsync isni1
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 author"
            Expect.equal filtered.[0].Isni isni1 "should have correct isni"
        }

        testCaseTask "filtering authors by isni and name - Ok" <| fun _ -> task {
            setUp ()
            let authorService = getAuthorService()
            let name = Name.New "John Smith"
            let name2 = Name.New "John Doe"
            let isni = Isni.New "0000 0001 2103 2683" |> Result.get
            let author1 = Author.New name isni
            let author2 = Author.New name2 (Isni.New "0000 0001 2103 2691" |> Result.get)
            
            let! _ = (authorService :> IAuthorService).AddAuthorsAsync [author1; author2]
            
            let! filteredResult = (authorService :> IAuthorService).SearchByIsniAndNameAsync (isni, name)
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 author"
            Expect.equal filtered.[0].AuthorId author1.AuthorId "should be author 1"
        }

        testCaseTask "add an author and then remove it - Ok" <| fun _ -> task {
            setUp ()
            let authorService = getAuthorService()
            let author = Author.NewWithoutIsni (Name.New "John Doe")
            
            let! _ = authorService.AddAuthorAsync author
            
            let! removeResult = (authorService :> IAuthorService).RemoveAsync author.AuthorId
            Expect.isOk removeResult "should be ok"
            
            let! getResult = authorService.GetAuthorAsync author.AuthorId
            Expect.isError getResult "should be error as it's removed"
        }

        testCaseTask "cannot remove an author that has books - Error" <| fun _ -> task {
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()
            let author = Author.NewWithoutIsni (Name.New "John Doe")
            
            let! _ = authorService.AddAuthorAsync author
            
            let book = Book.New (Title.New "The Great Gatsby") [author.AuthorId] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! _ = bookService.AddBookAsync book
            
            let! removeResult = (authorService :> IAuthorService).RemoveAsync author.AuthorId
            Expect.isError removeResult "should be error as author has books"
        }
    ]

    |> testSequenced
