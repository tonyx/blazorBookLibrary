
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
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Identity
open blazorBookLibrary.Data
open Sharpino.Cache
open BookLibrary.Details.Details

type TagService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        tagsViewerAsync: AggregateViewerAsync2<Tags>

    ) =
    new (secretsReader: SecretsReader) = 
        let connectionString = secretsReader.GetBookLibraryConnectionString()
        let messageSenders = MessageSenders.NoSender
        let eventStore = PgStorage.PgEventStore connectionString
        let tagsViewerAsync = getAggregateStorageFreshStateViewerAsync<Tags, TagEvent, string> eventStore
        TagService (eventStore, messageSenders, tagsViewerAsync)

    member private this.TagsRepoExists (?ct: CancellationToken) =
        let tagId = TagsId.UniqueTagId
        task {
            let! exists = tagsViewerAsync ct tagId.Value
            return exists.IsOk
        }

    // this will be called at startup
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

    member this.AddTagAsync (userContext: UserContext, tag: Tag, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let tagId = TagsId.UniqueTagId
            let addTagCommand = TagCommand.AddTag tag
            return!  
                runAggregateCommandMdAsync<Tags, TagEvent, string>
                    tagId.Value
                    eventStore
                    messageSenders
                    (userContext.ToString())
                    addTagCommand
                    (ct |> Some)
        }

    member this.RemoveTagAsync (userContext: UserContext, tag: Tag, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let tagId = TagsId.UniqueTagId
            let removeTagCommand = TagCommand.RemoveTag tag
            return!  
                runAggregateCommandMdAsync<Tags, TagEvent, string>
                    tagId.Value
                    eventStore
                    messageSenders
                    (userContext.ToString())
                    removeTagCommand
                    (ct |> Some)
        }

    member this.ReplaceTagAsync (userContext: UserContext, oldTag: Tag, newTag: Tag, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let tagId = TagsId.UniqueTagId
            let replaceTagCommand = TagCommand.ReplaceTag (oldTag, newTag)
            return!  
                runAggregateCommandMdAsync<Tags, TagEvent, string>
                    tagId.Value
                    eventStore
                    messageSenders
                    (userContext.ToString())
                    replaceTagCommand
                    (ct |> Some)
        }

    member this.GetTagsAsync(?ct: CancellationToken) =
        taskResult {
            let tagId = TagsId.UniqueTagId
            let! tags =
                tagsViewerAsync ct TagsId.UniqueTagId.Value
                |> TaskResult.map snd
            return tags.Tags
        }

    member this.GetBookTypeTagsAsync(?ct: CancellationToken) =
        taskResult {
            let! allTags = this.GetTagsAsync(?ct = ct)
            return allTags |> List.filter (fun t -> t.IsBookTag)
        }

    member this.GetAuthorTypeTagsAsync(?ct: CancellationToken) =
        taskResult {
            let! allTags = this.GetTagsAsync(?ct = ct)
            return allTags |> List.filter (fun t -> t.IsAuthorTag)
        }

    member this.GetGeneralTypeTagsAsync(?ct: CancellationToken) =
        taskResult {
            let! allTags = this.GetTagsAsync(?ct = ct)
            return allTags |> List.filter (fun t -> t.IsGeneralTag)
        }

    member this.GetPersonTypeTagsAsync(?ct: CancellationToken) =
        taskResult {
            let! allTags = this.GetTagsAsync(?ct = ct)
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
        member this.GetTagsAsync(?ct: CancellationToken): Tasks.Task<Result<Tag list,string>> = 
            this.GetTagsAsync(?ct = ct)
        member this.GetBookTypeTagsAsync(?ct: CancellationToken) =
            this.GetBookTypeTagsAsync(?ct = ct)
        member this.GetAuthorTypeTagsAsync(?ct: CancellationToken) =
            this.GetAuthorTypeTagsAsync(?ct = ct)
        member this.GetGeneralTypeTagsAsync(?ct: CancellationToken) =
            this.GetGeneralTypeTagsAsync(?ct = ct)
        member this.GetPersonTypeTagsAsync(?ct: CancellationToken) =
            this.GetPersonTypeTagsAsync(?ct = ct)
