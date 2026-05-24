namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Text.Json

type TenantEvent =
    | Deactivated 
    | Activated
    | ScheduledForDeletion of DateTime
    | TagAdded of Tag
    | TagRemoved of Tag
    | TagReplaced of Tag * Tag
    | PatronAdded of UserId * PatronRole
    | PatronDemoted of UserId
    | PatronPromoted of UserId
    | PatronRemoved of UserId
    | PatronInvited of UserId * PatronInvitationCode
    | PatronSuspended of UserId * string
    | PatronReadmitted of UserId
    | InvitedPatronConvertedToPatron of PatronInvitationCode
    | PatronInvitationRevoked of UserId
    | PublicSet
    | PrivateSet
    | JoinPinGenerated2 of string
    | JoinRequestSubmitted2 of UserId
    | JoinRequestApproved2 of UserId
    | JoinRequestRejected2 of UserId
    interface Event<Tenant> with
        member this.Process (tenant: Tenant) : Result<Tenant, string> =
            match this with
            | Deactivated ->
                tenant.Deactivate
            | Activated ->
                tenant.Activate
            | ScheduledForDeletion date ->
                tenant.ScheduleForDeletion date
            | TagAdded tag ->
                tenant.AddTag tag
            | TagRemoved tag ->
                tenant.RemoveTag tag
            | TagReplaced (oldTag, newTag) ->
                tenant.ReplaceTag (oldTag, newTag)
            | PatronAdded (userId, role) ->
                tenant.AddPatron (userId, role)
            | PatronDemoted userId ->
                tenant.DemotePatron userId
            | PatronPromoted userId ->
                tenant.PromotePatron userId
            | PatronRemoved userId ->
                tenant.RemovePatron userId
            | PatronInvited (userId, invitationCode) ->
                tenant.InvitePatron (userId, invitationCode)
            | InvitedPatronConvertedToPatron invitationCode ->
                tenant.ConvertInvitedPatronToPatron invitationCode    
            | PatronSuspended (userId, reason) ->
                tenant.SuspendPatron (userId, reason)
            | PatronReadmitted userId ->
                tenant.ReAdmittPatron userId
            | PublicSet ->
                tenant.SetPublic ()
            | PrivateSet ->
                tenant.SetPrivate ()
            | PatronInvitationRevoked userId ->
                tenant.RevokeInvitation userId
            | JoinPinGenerated2 pin ->
                tenant.GenerateJoinPin2 pin
            | JoinRequestSubmitted2 userId ->
                tenant.AddJoinRequest2 userId
            | JoinRequestApproved2 userId ->
                tenant.ApproveJoinRequest2 userId
            | JoinRequestRejected2 userId ->
                tenant.RejectJoinRequest2 userId

    static member Deserialize (x: string): Result<TenantEvent, string> =
        try
            JsonSerializer.Deserialize<TenantEvent> (x, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message
    
    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)
