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

open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Utils
open Microsoft.Extensions.DependencyInjection

type EditorService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        editorViewerAsync: AggregateViewerAsync2<Editor>,
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

    new
        (
            eventStore: IEventStore<string>,
            secretsReader: SecretsReader,
            userTenantResolverService: IUserTenantResolverService
        ) =
        EditorService(
            eventStore,
            MessageSenders.NoSender,
            getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore,
            userTenantResolverService,
            secretsReader
        )

    [<ActivatorUtilitiesConstructor>]
    new(secretsReader: SecretsReader, userTenantResolverService: IUserTenantResolverService) =
        EditorService(
            PgStorage.PgEventStore(secretsReader.GetBookLibraryConnectionString()),
            secretsReader,
            userTenantResolverService
        )

    member this.AddEditorAsync(context: UserContext, editor: Editor, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            do! tenantId = editor.TenantId |> Result.ofBool "Tenant ids not matching"
            do! checkIsGlobalAdminOrTenantManager context ct

            do! runInitAsync<Editor, EditorEvent, string> eventStore messageSenders editor (Some ct)
            return editor
        }

    member this.GetEditorAsync(context: UserContext, id: EditorId, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! (_, editor) = editorViewerAsync (Some ct) id.Value
            do! editor.TenantId = tenantId |> Result.ofBool "Tenant ids not matching"
            return editor
        }

    member this.RenameAsync(context: UserContext, editorId: EditorId, newName: Name, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let! editor = this.GetEditorAsync(context, editorId, ct)
            do! checkIsGlobalAdminOrTenantManager context ct

            let cmd = EditorCommand.UpdateName(newName, DateTime.UtcNow)
            let! _ =
                runAggregateCommandMdAsync<Editor, EditorEvent, string>
                    editorId.Value
                    eventStore
                    messageSenders
                    context.Metadata
                    cmd
                    (Some ct)

            return! this.GetEditorAsync(context, editorId, ct)
        }

    member this.GetAllAsync(context: UserContext, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! allEditors =
                StateView.getAllFilteredAggregateStatesAsync<Editor, EditorEvent, string>
                    (fun editor -> editor.TenantId = tenantId)
                    eventStore
                    (Some ct)
            return allEditors |> List.ofSeq |> List.map snd
        }

    interface IEditorService with
        member this.AddEditorAsync(context, editor, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            this.AddEditorAsync(context, editor, ct)

        member this.GetEditorAsync(context, id, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            this.GetEditorAsync(context, id, ct)

        member this.RenameAsync(context, editorId, newName, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            this.RenameAsync(context, editorId, newName, ct)

        member this.GetAllAsync(context, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            this.GetAllAsync(context, ct)
