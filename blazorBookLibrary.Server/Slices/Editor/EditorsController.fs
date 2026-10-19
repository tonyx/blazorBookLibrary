namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Services
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Domain
open System.Threading.Tasks
open System
open System.Collections.Generic

[<ApiController>]
[<Route("api/[controller]")>]
type EditorsController(editorService: IEditorService, hubContext: Microsoft.AspNetCore.SignalR.IHubContext<BookLibrary.Hubs.LibraryHub>) =
    inherit ControllerBase()

    [<HttpPost>]
    member this.AddEditor([<FromBody>] editor: Editor) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = editorService.AddEditorAsync(context, editor)
            match result with
            | Ok created -> 
                do! hubContext.Clients.All.SendCoreAsync("EditorsChanged", [||])
                return this.Ok(created) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("{id}")>]
    member this.GetEditor(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = editorService.GetEditorAsync(context, EditorId id)
            match result with
            | Ok editor -> return this.Ok(editor) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpPost("{id}/rename")>]
    member this.Rename(id: Guid, [<FromBody>] newName: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = editorService.RenameAsync(context, EditorId id, Name.New newName)
            match result with
            | Ok updated ->
                do! hubContext.Clients.All.SendCoreAsync("EditorsChanged", [||])
                return this.Ok(updated) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet>]
    member this.GetAll() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = editorService.GetAllAsync(context)
            match result with
            | Ok editors -> return this.Ok(editors) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
