namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons

type TenantCommand =
    | Deactivate
    | Activate
    | ScheduleForDeletion of DateTime
    | AddTag of Tag
    | RemoveTag of Tag
    | ReplaceTag of (Tag * Tag)
    | AddPatron of (UserId * PatronRole)
    | DemotePatron of UserId
    | PromotePatron of UserId
    | RemovePatron of UserId
    | InvitePatron of UserId * PatronInvitationCode
    | ConvertInvitedPatronToPatron of PatronInvitationCode
    | RevokePatronInvitation of UserId
    | SuspendPatron of UserId * string
    | ReadmittPatron of UserId
    | SetPublic
    | SetPrivate
    interface AggregateCommand<Tenant, TenantEvent> with
        member this.Execute (tenant: Tenant) =
            match this with
            | Deactivate ->
                tenant.Deactivate
                |> Result.map (fun t -> (t, [TenantEvent.Deactivated]))
            | Activate ->
                tenant.Activate
                |> Result.map (fun t -> (t, [TenantEvent.Activated]))
            | ScheduleForDeletion date ->
                tenant.ScheduleForDeletion date
                |> Result.map (fun t -> (t, [TenantEvent.ScheduledForDeletion date]))
            | AddTag tag ->
                tenant.AddTag tag
                |> Result.map (fun t -> (t, [TenantEvent.TagAdded tag]))
            | RemoveTag tag ->
                tenant.RemoveTag tag
                |> Result.map (fun t -> (t, [TenantEvent.TagRemoved tag]))
            | ReplaceTag (oldTag, newTag) ->
                tenant.ReplaceTag (oldTag, newTag)
                |> Result.map (fun t -> (t, [TenantEvent.TagReplaced (oldTag, newTag)]))
            | AddPatron (userId, role) ->
                tenant.AddPatron (userId, role)
                |> Result.map (fun t -> (t, [TenantEvent.PatronAdded (userId, role)]))
            | DemotePatron userId ->
                tenant.DemotePatron userId
                |> Result.map (fun t -> (t, [TenantEvent.PatronDemoted userId]))
            | PromotePatron userId ->
                tenant.PromotePatron userId
                |> Result.map (fun t -> (t, [TenantEvent.PatronPromoted userId]))
            | RemovePatron userId ->
                tenant.RemovePatron userId
                |> Result.map (fun t -> (t, [TenantEvent.PatronRemoved userId]))
            | InvitePatron (userId, invitationCode) ->
                tenant.InvitePatron (userId, invitationCode)
                |> Result.map (fun t -> (t, [TenantEvent.PatronInvited (userId, invitationCode)]))
            | ConvertInvitedPatronToPatron invitationCode ->
                tenant.ConvertInvitedPatronToPatron invitationCode
                |> Result.map (fun t -> (t, [TenantEvent.InvitedPatronConvertedToPatron invitationCode]))
            | RevokePatronInvitation userId ->
                tenant.RevokeInvitation userId
                |> Result.map (fun t -> (t, [TenantEvent.PatronInvitationRevoked userId]))
            | SuspendPatron (userId, reason) ->
                tenant.SuspendPatron(userId, reason)
                |> Result.map (fun t -> (t, [TenantEvent.PatronSuspended(userId, reason)]))
            | ReadmittPatron userId ->
                tenant.ReAdmittPatron(userId)
                |> Result.map (fun t -> (t, [TenantEvent.PatronReadmitted(userId)]))
            | SetPublic ->
                tenant.SetPublic()
                |> Result.map (fun t -> (t, [TenantEvent.PublicSet]))
            | SetPrivate ->
                tenant.SetPrivate()
                |> Result.map (fun t -> (t, [TenantEvent.PrivateSet]))
        member this.Undoer = None
