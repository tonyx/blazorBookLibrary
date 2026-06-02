namespace BookLibrary.Services

open System.Threading
open System
open Sharpino
open Sharpino.Cache
open FSharpPlus.Operators
open Sharpino.CommandHandler
open Sharpino.EventBroker
open Sharpino.Storage
open Sharpino.StateView

open BookLibrary.Domain
open FsToolkit.ErrorHandling

open BookLibrary.Shared.Details
open BookLibrary.Details.Details

open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Utils

type AuthorService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        bookViewerAsync: AggregateViewerAsync2<Book>,
        authorViewerAsync: AggregateViewerAsync2<Author>,
        editorViewerAsync: AggregateViewerAsync2<Editor>,
        reservationViewerAsync: AggregateViewerAsync2<Reservation>,
        loanViewerAsync: AggregateViewerAsync2<Loan>,
        tenantViewerAsync: AggregateViewerAsync2<Tenant>,
        userTenantResolverService: IUserTenantResolverService,
        secretsReader: SecretsReader
    ) =

    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken) =
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }

    let checkIsGlobalAdminOrTenantManagerOrPublicTenant (context: UserContext) (ct: CancellationToken) =
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManagerOrPublicTenant tenant context
        }

    let checkIsGlobalAdminOrTenantManagerOrSelf (context: UserContext) (ct: CancellationToken) (userId: UserId) =
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManagerOrSelf tenant context userId
        }

    member this.AddAuthorAsync(context: UserContext, author: Author, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            do! tenantId = author.TenantId |> Result.ofBool "Tenant ids not matching"
            do! checkIsGlobalAdminOrTenantManager context ct

            return! runInitAsync<Author, AuthorEvent, string> eventStore messageSenders author (Some ct)
        }

    member this.AddAuthorsAsync(context: UserContext, authors: list<Author>, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)

            do!
                authors
                |> Seq.forall (fun a -> tenantId = a.TenantId)
                |> Result.ofBool "Tenant ids not matching"

            return!
                runMultipleInitAsync<Author, AuthorEvent, string>
                    eventStore
                    messageSenders
                    (authors |> Array.ofList)
                    (Some ct)
        }

    member this.GetAuthorAsync(context: UserContext, authorId: AuthorId, ?ct: CancellationToken) =
        taskResult { return! authorViewerAsync ct authorId.Value |> TaskResult.map snd }

    member private this.GetRefreshableAuthorDetailsAsync(context: UserContext, id: AuthorId, ?ct: CancellationToken) =
        let detailsBuilder =
            fun (ct: Option<CancellationToken>) ->
                let refresher =
                    fun (ct: Option<CancellationToken>) ->
                        taskResult {
                            let ct = defaultArg ct CancellationToken.None
                            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
                            let! author = authorViewerAsync (Some ct) id.Value |> TaskResult.map snd

                            let! booksWithId =
                                StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string>
                                    (fun book -> book.TenantId = tenantId && (book.Authors |> List.contains id))
                                    eventStore
                                    (Some ct)

                            let books = booksWithId |> List.ofSeq |> List.map snd
                            do! author.TenantId = tenantId |> Result.ofBool "Tenant ids not matching"
                            return { Author = author; Books = books }
                        }

                taskResult {
                    let! authorDetails = refresher ct

                    return
                        { AuthorDetails = authorDetails
                          Refresher = refresher }
                        :> RefreshableAsync<RefreshableAuthorDetails>,
                        id.Value :: (authorDetails.Books |> List.map (fun book -> book.BookId.Value))
                }

        let key = DetailsCacheKey.OfType typeof<RefreshableAuthorDetails> id.Value
        StateView.getRefreshableDetailsTaskResultAsync<RefreshableAuthorDetails> (fun ct -> detailsBuilder ct) key ct

    member this.GetAuthorDetailsAsync(context: UserContext, authorId: AuthorId, ?ct: CancellationToken) =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            let! refreshableAuthorDetails = this.GetRefreshableAuthorDetailsAsync(context, authorId, ct)
            return refreshableAuthorDetails.AuthorDetails
        }

    member this.GetAuthorBooksAsync(context: UserContext, authorId: AuthorId, ?ct: CancellationToken) =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)

            let filter (book: Book) =
                book.TenantId = tenantId && (book.Authors |> List.contains authorId)

            let! booksWithId =
                StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore (Some ct)

            return booksWithId |> List.ofSeq |> List.map snd
        }

    member this.GetAuthorsAsync(context: UserContext, ids: List<AuthorId>, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! authors = ids |> List.traverseTaskResultM (fun id -> this.GetAuthorAsync(context, id, ct))

            do!
                authors
                |> Seq.forall (fun a -> tenantId = a.TenantId)
                |> Result.ofBool "Tenant ids not matching"

            return authors
        }

    member this.RenameAsync(context: UserContext, authorId: AuthorId, newName: Name, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! author = this.GetAuthorAsync(context, authorId, ct)
            do! tenantId = author.TenantId |> Result.ofBool "Tenant ids not matching"

            let reamecommand = AuthorCommand.Rename(newName, DateTime.UtcNow)

            let result =
                runAggregateCommandMdAsync<Author, AuthorEvent, string>
                    authorId.Value
                    eventStore
                    messageSenders
                    (context.ToString())
                    reamecommand
                    (Some ct)

            return! result
        }

    member this.UpdateIsniAsync(context: UserContext, authorId: AuthorId, isni: Isni, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! author = this.GetAuthorAsync(context, authorId, ct)
            do! tenantId = author.TenantId |> Result.ofBool "Tenant ids not matching"
            do! checkIsGlobalAdminOrTenantManager context ct
            let updateIsniCommand = AuthorCommand.UpdateIsni(isni, DateTime.UtcNow)

            let result =
                runAggregateCommandMdAsync<Author, AuthorEvent, string>
                    authorId.Value
                    eventStore
                    messageSenders
                    (context.ToString())
                    updateIsniCommand
                    (Some ct)

            return! result
        }

    member this.UpdateBioAsync(context: UserContext, authorId: AuthorId, bio: string, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! author = this.GetAuthorAsync(context, authorId, ct)
            do! tenantId = author.TenantId |> Result.ofBool "Tenant ids not matching"
            do! checkIsGlobalAdminOrTenantManager context ct
            let updateBioCommand = AuthorCommand.UpdateBio(bio, DateTime.UtcNow)

            let result =
                runAggregateCommandMdAsync<Author, AuthorEvent, string>
                    authorId.Value
                    eventStore
                    messageSenders
                    (context.ToString())
                    updateBioCommand
                    (Some ct)

            return! result
        }

    member this.UpdateWikipediaUriAsync
        (context: UserContext, authorId: AuthorId, wikipediaUri: Uri, ?ct: CancellationToken)
        =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! author = this.GetAuthorAsync(context, authorId, ct)
            do! tenantId = author.TenantId |> Result.ofBool "Tenant ids not matching"
            do! checkIsGlobalAdminOrTenantManager context ct

            let updateWikipediaUriCommand =
                AuthorCommand.UpdateWikipediaUri(wikipediaUri, DateTime.UtcNow)

            let result =
                runAggregateCommandMdAsync<Author, AuthorEvent, string>
                    authorId.Value
                    eventStore
                    messageSenders
                    (context.ToString())
                    updateWikipediaUriCommand
                    (Some ct)

            return! result
        }

    member this.UpdateImageUrlAsync(context: UserContext, authorId: AuthorId, imageUrl: Uri, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! author = this.GetAuthorAsync(context, authorId, ct)
            do! tenantId = author.TenantId |> Result.ofBool "Tenant ids not matching"
            do! checkIsGlobalAdminOrTenantManager context ct
            let updateImageUrlCommand = AuthorCommand.UpdateImageUrl(imageUrl, DateTime.UtcNow)

            let result =
                runAggregateCommandMdAsync<Author, AuthorEvent, string>
                    authorId.Value
                    eventStore
                    messageSenders
                    (context.ToString())
                    updateImageUrlCommand
                    (Some ct)

            return! result
        }

    member this.RemoveImageUrlAsync(context: UserContext, authorId: AuthorId, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! author = this.GetAuthorAsync(context, authorId, ct)
            do! tenantId = author.TenantId |> Result.ofBool "Tenant ids not matching"
            do! checkIsGlobalAdminOrTenantManager context ct
            let removeImageUrlCommand = AuthorCommand.RemoveImageUrl(DateTime.UtcNow)

            let result =
                runAggregateCommandMdAsync<Author, AuthorEvent, string>
                    authorId.Value
                    eventStore
                    messageSenders
                    (context.ToString())
                    removeImageUrlCommand
                    (Some ct)

            return! result
        }

    member this.SealAsync(context: UserContext, authorId: AuthorId, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! author = this.GetAuthorAsync(context, authorId, ct)
            do! tenantId = author.TenantId |> Result.ofBool "Tenant ids not matching"
            do! checkIsGlobalAdminOrTenantManager context ct
            let sealCommand = AuthorCommand.Seal(DateTime.UtcNow)

            let result =
                runAggregateCommandMdAsync<Author, AuthorEvent, string>
                    authorId.Value
                    eventStore
                    messageSenders
                    (context.ToString())
                    sealCommand
                    (Some ct)

            return! result
        }

    member this.UnsealAsync(context: UserContext, authorId: AuthorId, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! author = this.GetAuthorAsync(context, authorId, ct)
            do! tenantId = author.TenantId |> Result.ofBool "Tenant ids not matching"
            do! checkIsGlobalAdminOrTenantManager context ct
            let unsealCommand = AuthorCommand.Unseal(DateTime.UtcNow)

            let result =
                runAggregateCommandMdAsync<Author, AuthorEvent, string>
                    authorId.Value
                    eventStore
                    messageSenders
                    (context.ToString())
                    unsealCommand
                    (Some ct)

            return! result
        }

    member this.RemoveAuthorAsync(context: UserContext, authorId: AuthorId, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! author = this.GetAuthorAsync(context, authorId, ct)
            let! authorsBooks = this.GetAuthorBooksAsync(context, authorId, ct)
            do! tenantId = author.TenantId |> Result.ofBool "Tenant ids not matching"
            do! checkIsGlobalAdminOrTenantManager context ct
            let! books = this.GetAuthorBooksAsync(context, authorId, ct)
            do! books.IsEmpty |> Result.ofBool "Cannot remove an author that has books"

            return!
                runDeleteAsync<Author, AuthorEvent, string>
                    eventStore
                    messageSenders
                    authorId.Value
                    (fun _ -> authorsBooks.Length = 0)
                    (Some ct)
        }

    member this.GetAllAuthorsAsync(context: UserContext, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! authorsWithId = getAllAggregateStatesAsync<Author, AuthorEvent, string> eventStore (Some ct)

            return
                authorsWithId
                |> List.ofSeq
                |> List.map snd
                |> List.filter (fun a -> tenantId = a.TenantId)
        }

    member this.GetAllAuthorsOfTenantAsync(context: UserContext, tenantId: TenantId, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            do! checkIsGlobalAdminOrTenantManager context ct

            let! authorsWithId =
                getAllFilteredAggregateStatesAsync<Author, AuthorEvent, string>
                    (fun a -> a.TenantId = tenantId)
                    eventStore
                    (Some ct)

            return authorsWithId |> List.ofSeq |> List.map snd
        }


    member this.GetAllAuthorsFilteredByName(context: UserContext, name: Name, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)

            let filter (author: Author) =
                tenantId = author.TenantId
                && author.Name.Value.Contains(name.Value, StringComparison.OrdinalIgnoreCase)

            let! authorsWithId =
                getAllFilteredAggregateStatesAsync<Author, AuthorEvent, string> filter eventStore (Some ct)

            return
                authorsWithId
                |> List.ofSeq
                |> List.map snd
                |> List.filter (fun a -> tenantId = a.TenantId)
        }


    member this.GetAllAuthorsFilteredByIsni(context: UserContext, isni: Isni, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)

            let filter (author: Author) =
                tenantId = author.TenantId
                && author.Isni.Value.Contains(isni.Value, StringComparison.OrdinalIgnoreCase)

            let! authorsWithId =
                getAllFilteredAggregateStatesAsync<Author, AuthorEvent, string> filter eventStore (Some ct)

            return
                authorsWithId
                |> List.ofSeq
                |> List.map snd
                |> List.filter (fun a -> tenantId = a.TenantId)
        }

    member this.GetAllAuthorsFilteredByIsniAndName
        (context: UserContext, isni: Isni, name: Name, ?ct: CancellationToken)
        =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)

            let filter (author: Author) =
                tenantId = author.TenantId
                && (author.Isni.Value.Contains(isni.Value, StringComparison.OrdinalIgnoreCase)
                    || author.Name.Value.Contains(name.Value, StringComparison.OrdinalIgnoreCase))

            let! authorsWithId =
                getAllFilteredAggregateStatesAsync<Author, AuthorEvent, string> filter eventStore (Some ct)

            return
                authorsWithId
                |> List.ofSeq
                |> List.map snd
                |> List.filter (fun a -> tenantId = a.TenantId)
        }

    new
        (
            eventStore: IEventStore<string>,
            secretsReader: SecretsReader,
            userTenantResolverService: IUserTenantResolverService
        ) =
        AuthorService(
            eventStore,
            MessageSenders.NoSender,
            getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore,
            userTenantResolverService,
            secretsReader
        )

    new(secretsReader: SecretsReader, userTenantResolverService: IUserTenantResolverService) =
        AuthorService(
            PgStorage.PgEventStore(secretsReader.GetBookLibraryConnectionString()),
            secretsReader,
            userTenantResolverService
        )

    interface IAuthorService with
        member this.AddAuthorAsync(context, author: Author, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.AddAuthorAsync(context, author, ct)

        member this.AddAuthorsAsync(context, authors: list<Author>, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.AddAuthorsAsync(context, authors, ct)

        member this.GetAuthorAsync(context, authorId: AuthorId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetAuthorAsync(context, authorId, ct)

        member this.GetAuthorDetailsAsync(context, authorId: AuthorId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetAuthorDetailsAsync(context, authorId, ct)

        member this.GetAuthorsAsync(context, ids: List<AuthorId>, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetAuthorsAsync(context, ids, ct)

        member this.GetAuthorBooksAsync(context, authorId: AuthorId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetAuthorBooksAsync(context, authorId, ct)

        member this.RenameAsync(context, authorId: AuthorId, newName: Name, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.RenameAsync(context, authorId, newName, ct)

        member this.RemoveAsync(context, authorId: AuthorId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.RemoveAuthorAsync(context, authorId, ct)

        member this.UpdateIsniAsync(context, authorId: AuthorId, isni: Isni, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.UpdateIsniAsync(context, authorId, isni, ct)

        member this.UpdateBioAsync(context, authorId: AuthorId, bio: string, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.UpdateBioAsync(context, authorId, bio, ct)

        member this.UpdateWikipediaUriAsync(context, authorId: AuthorId, wikipediaUri: Uri, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.UpdateWikipediaUriAsync(context, authorId, wikipediaUri, ct)

        member this.UpdateImageUrlAsync(context, authorId: AuthorId, imageUrl: Uri, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.UpdateImageUrlAsync(context, authorId, imageUrl, ct)

        member this.RemoveImageUrlAsync(context, authorId: AuthorId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.RemoveImageUrlAsync(context, authorId, ct)

        member this.SealAsync(context, authorId: AuthorId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.SealAsync(context, authorId, ct)

        member this.UnsealAsync(context, authorId: AuthorId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.UnsealAsync(context, authorId, ct)

        member this.GetAllAsync(context, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetAllAuthorsAsync(context, ct)

        member this.GetAllAuthorsOfTenantAsync(context, tenantId: TenantId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetAllAuthorsOfTenantAsync(context, tenantId, ct)

        member this.SearchByNameAsync(context, name: Name, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetAllAuthorsFilteredByName(context, name, ct)

        member this.SearchByIsniAsync(context, isni: Isni, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetAllAuthorsFilteredByIsni(context, isni, ct)

        member this.SearchByIsniAndNameAsync(context, isni: Isni, name: Name, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetAllAuthorsFilteredByIsniAndName(context, isni, name, ct)
