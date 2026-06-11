namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Domain
open System
open System.Threading.Tasks
open BookLibrary.Services
open BookLibrary.Shared

[<ApiController>]
[<Route("api/[controller]")>]
type NotificationsController(notificationService: INotificationService) =
    inherit ControllerBase()

    [<HttpGet("unread")>]
    member this.GetUnreadNotifications() =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = notificationService.GetUnreadNotificationsForUserAsync(context)
            match result with
            | Ok notifications -> return this.Ok(notifications) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("all")>]
    member this.GetAllNotifications() =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = notificationService.GetAllNotificationsForUserAsync(context)
            match result with
            | Ok notifications -> return this.Ok(notifications) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPut("{id}/read")>]
    member this.MarkAsRead(id: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = notificationService.MarkAsReadAsync(context, NotificationId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost>]
    member this.CreateNotification([<FromBody>] notification: Notification) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = notificationService.CreateNotificationAsync(context, notification)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
