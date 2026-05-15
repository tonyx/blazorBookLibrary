
namespace BookLibrary.Services
open System.Threading
open System
open Sharpino
open Sharpino.CommandHandler
open Sharpino
open Sharpino.Cache
open FSharpPlus.Operators
open Sharpino.CommandHandler
open Sharpino.EventBroker
open Sharpino.Storage
open Sharpino.EventBroker
open Sharpino.Storage
open BookLibrary.Domain
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Utils

// tag structure will not fint well in to the tenant system because it is a single aggregate 
type TagService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        userViewerAsync: AggregateViewerAsync2<User>,
        tenantViewerAsync: AggregateViewerAsync2<Tenant>,
        tagsViewerAsync: AggregateViewerAsync2<Tags>

    ) =

    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken)= 
        taskResult {
            let! tenant = tenantViewerAsync (ct |> Some) context.TenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }
    let checkIsGlobalAdminOrTenantManagerOrPublicTenant (context: UserContext) (ct: CancellationToken)= 
        taskResult {
            let! tenant = tenantViewerAsync (ct |> Some) context.TenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManagerOrPublicTenant tenant context
        }

    new (secretsReader: SecretsReader) = 
        let connectionString = secretsReader.GetBookLibraryConnectionString()
        let messageSenders = MessageSenders.NoSender
        let eventStore = PgStorage.PgEventStore connectionString
        let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
        let tenantViewerAsync = getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore
        let tagsViewerAsync = getAggregateStorageFreshStateViewerAsync<Tags, TagEvent, string> eventStore
        TagService (eventStore, messageSenders, userViewerAsync, tenantViewerAsync, tagsViewerAsync)

    member private this.TagsRepoExists (?ct: CancellationToken) =
        let tagId = TagsId.UniqueTagId
        task {
            let! exists = tagsViewerAsync ct tagId.Value
            return exists.IsOk
        }

    // this will be called at startup. Note we will remove this stuff as tags are now part of the tenant
    member this.EnsureTagsRepoCreatedAsync (?ct: CancellationToken) =
        taskResult {
            let tagId = TagsId.UniqueTagId
            let! exists = this.TagsRepoExists (?ct = ct)
            if (not exists) then
                let initialInstance = Tags.New
                let! result = 
                    runInitAsync<Tags, TagEvent, string>
                        eventStore
                        messageSenders
                        initialInstance
                        ct
                return result
            else
                return ()
        }

    member this.AddTagAsync (context: UserContext, tag: Tag, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None
        taskResult {

            let tenantId = context.TenantId
            let addTagCommand = TenantCommand.AddTag tag
            return!  
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    (context.ToString())
                    addTagCommand
                    (ct |> Some)
        }

    member this.RemoveTagAsync (context: UserContext, tag: Tag, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let tenantId = context.TenantId
            do! checkIsGlobalAdminOrTenantManager context ct
            let removeTagCommand = TenantCommand.RemoveTag tag
            return!  
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    (context.ToString())
                    removeTagCommand
                    (ct |> Some)
        }

    member this.ReplaceTagAsync (userContext: UserContext, oldTag: Tag, newTag: Tag, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let tenantId = userContext.TenantId
            do! checkIsGlobalAdminOrTenantManager userContext ct
            let replaceTagCommand = TenantCommand.ReplaceTag (oldTag, newTag)
            return!  
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    (userContext.ToString())
                    replaceTagCommand
                    (ct |> Some)
        }

    member this.GetTagsAsync(context: UserContext, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let tenantId = context.TenantId
            do! checkIsGlobalAdminOrTenantManagerOrPublicTenant context ct
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return tenant.Tags
        }

    member this.GetBookTypeTagsAsync(context: UserContext, ?ct: CancellationToken) =
        taskResult {
            let! allTags = this.GetTagsAsync(context, ?ct = ct)
            return allTags |> List.filter (fun t -> t.IsBookTag)
        }

    member this.GetAuthorTypeTagsAsync(context: UserContext, ?ct: CancellationToken) =
        taskResult {
            let! allTags = this.GetTagsAsync(context, ?ct = ct)
            return allTags |> List.filter (fun t -> t.IsAuthorTag)
        }

    member this.GetGeneralTypeTagsAsync(context: UserContext, ?ct: CancellationToken) =
        taskResult {
            let! allTags = this.GetTagsAsync(context, ?ct = ct)
            return allTags |> List.filter (fun t -> t.IsGeneralTag)
        }

    member this.GetPersonTypeTagsAsync(context: UserContext, ?ct: CancellationToken) =
        taskResult {
            let! allTags = this.GetTagsAsync(context, ?ct = ct)
            return allTags |> List.filter (fun t -> t.IsPersonTag)
        }

    interface ITagService with
        member this.EnsureTagsRepoCreatedAsync (?ct: CancellationToken) =
            this.EnsureTagsRepoCreatedAsync (?ct = ct)

        member this.AddTagAsync (userContext:UserContext, tag: Tag, ?ct: CancellationToken) =
            this.AddTagAsync (userContext, tag, ?ct = ct)

        member this.RemoveTagAsync (userContext:UserContext, tag: Tag, ?ct: CancellationToken) =
            this.RemoveTagAsync (userContext, tag, ?ct = ct)

        member this.ReplaceTagAsync (userContext:UserContext, oldTag: Tag, newTag: Tag, ?ct: CancellationToken) =
            this.ReplaceTagAsync (userContext, oldTag, newTag, ?ct = ct)            
        member this.GetTagsAsync(context: UserContext, ?ct: CancellationToken): Tasks.Task<Result<Tag list,string>> = 
            this.GetTagsAsync(context, ?ct = ct)
        member this.GetBookTypeTagsAsync(context: UserContext, ?ct: CancellationToken) =
            this.GetBookTypeTagsAsync(context, ?ct = ct)
        member this.GetAuthorTypeTagsAsync(context: UserContext, ?ct: CancellationToken) =
            this.GetAuthorTypeTagsAsync(context, ?ct = ct)
        member this.GetGeneralTypeTagsAsync(context: UserContext, ?ct: CancellationToken) =
            this.GetGeneralTypeTagsAsync(context, ?ct = ct)
        member this.GetPersonTypeTagsAsync(context: UserContext, ?ct: CancellationToken) =
            this.GetPersonTypeTagsAsync(context, ?ct = ct)
