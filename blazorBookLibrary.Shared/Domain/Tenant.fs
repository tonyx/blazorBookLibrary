namespace BookLibrary.Domain

open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons
open Sharpino
open System

type PatronRole =
    | Manager
    | User
    | Suspended of string

type Tenant001 =
    { OwnerId: UserId
      TenantId: TenantId
      Patrons: List<UserId * PatronRole>
      TentantName: TenantName
      Address: string
      TenantState: TenantState
      Public: bool
      Tags: List<Tag> }

    member this.Upcast() : Tenant =
        { OwnerId = this.OwnerId
          TenantId = this.TenantId
          InvitedPatrons = []
          Patrons = this.Patrons
          Name = this.TentantName
          Address = this.Address
          TenantState = this.TenantState
          Public = this.Public
          Tags = this.Tags
          CurrentJoinPin = None
          JoinRequests = [] }

and Tenant002 =
    { OwnerId: UserId
      TenantId: TenantId
      InvitedPatrons: List<(UserId * PatronInvitationCode)>
      Patrons: List<UserId * PatronRole>
      Name: TenantName
      Address: string
      TenantState: TenantState
      Public: bool
      Tags: List<Tag> }

    member this.Upcast() : Tenant =
        { OwnerId = this.OwnerId
          TenantId = this.TenantId
          InvitedPatrons = this.InvitedPatrons
          Patrons = this.Patrons
          Name = this.Name
          Address = this.Address
          TenantState = this.TenantState
          Public = this.Public
          Tags = this.Tags
          CurrentJoinPin = None
          JoinRequests = [] }

and Tenant =
    { OwnerId: UserId
      TenantId: TenantId
      InvitedPatrons: List<(UserId * PatronInvitationCode)>
      Patrons: List<UserId * PatronRole>
      Name: TenantName
      Address: string
      TenantState: TenantState
      Public: bool
      Tags: List<Tag>
      CurrentJoinPin: Option<string>
      JoinRequests: List<UserId> }

    static member New(userId: UserId, tenantName: TenantName, address: string, ?pub: bool) =
        { OwnerId = userId
          TenantId = TenantId.New()
          InvitedPatrons = []
          Patrons = []
          Name = tenantName
          Address = address
          TenantState = TenantState.Active
          Public = pub |> Option.defaultValue false
          Tags = []
          CurrentJoinPin = None
          JoinRequests = [] }

    static member NewDefault(userId: UserId, tenantName: TenantName, address: string) =
        { Tenant.New(userId, tenantName, address, true) with
            TenantId = TenantId.Default }

    member this.GenerateJoinPin2 (pin: string) =
        { this with CurrentJoinPin = Some pin } |> Ok

    member this.AddJoinRequest2 (userId: UserId) =
        result {
            do!
                this.Patrons
                |> List.exists (fun (u, _) -> u = userId)
                |> not
                |> Result.ofBool "User is already a patron of this library"
            do!
                this.JoinRequests
                |> List.contains userId
                |> not
                |> Result.ofBool "User has already requested to join this library"
            return { this with JoinRequests = this.JoinRequests @ [ userId ] }
        }

    member this.ApproveJoinRequest2 (userId: UserId) =
        result {
            do!
                this.JoinRequests
                |> List.contains userId
                |> Result.ofBool "Join request not found"
            return 
                { this with
                    Patrons = this.Patrons @ [ (userId, PatronRole.User) ]
                    JoinRequests = this.JoinRequests |> List.filter (fun u -> u <> userId) }
        }

    member this.RejectJoinRequest2 (userId: UserId) =
        result {
            do!
                this.JoinRequests
                |> List.contains userId
                |> Result.ofBool "Join request not found"
            return 
                { this with
                    JoinRequests = this.JoinRequests |> List.filter (fun u -> u <> userId) }
        }

    member this.Deactivate =
        match this.TenantState with
        | Active ->
            { this with
                TenantState = TenantState.Deactivated }
            |> Ok
        | _ -> Error "Tenant is already deactivated"

    member this.AddTag(tag: Tag) =
        result {
            do!
                this.Tags
                |> List.exists (fun t -> t = tag)
                |> not
                |> Result.ofBool "Tag already exists"

            return { this with Tags = this.Tags @ [ tag ] }
        }

    member this.RemoveTag(tag: Tag) =
        result {
            do!
                this.Tags
                |> List.exists (fun t -> t = tag)
                |> Result.ofBool "Tag does not exist"

            return
                { this with
                    Tags = this.Tags |> List.filter (fun t -> t <> tag) }
        }

    member this.AddPatron(user: UserId, role: PatronRole) =
        result {
            do!
                this.Patrons
                |> List.exists (fun (u, _) -> u = user)
                |> not
                |> Result.ofBool "User is already a patron"

            return
                { this with
                    Patrons = this.Patrons @ [ (user, role) ] }
        }

    member this.InvitePatron(user: UserId, invitationCode: PatronInvitationCode) =
        result {
            do!
                this.Patrons
                |> List.exists (fun (u, _) -> u = user)
                |> not
                |> Result.ofBool "User is already a patron"

            return
                { this with
                    InvitedPatrons = this.InvitedPatrons @ [ (user, invitationCode) ] }
        }

    member this.RevokeInvitation(userId: UserId) =
        result {
            do!
                this.InvitedPatrons
                |> List.exists (fun (u, _) -> u = userId)
                |> Result.ofBool "User is not an invited patron"

            return
                { this with
                    InvitedPatrons = this.InvitedPatrons |> List.filter (fun (u, _) -> u <> userId) }
        }

    member this.ConvertInvitedPatronToPatron(invitationCode: PatronInvitationCode) =
        result {
            do!
                this.InvitedPatrons
                |> List.exists (fun (_, i) -> i = invitationCode)
                |> Result.ofBool "User is not an invited patron"

            let user =
                this.InvitedPatrons |> List.find (fun (_, i) -> i = invitationCode) |> fst

            return
                { this with
                    Patrons = this.Patrons @ [ (user, PatronRole.User) ]
                    InvitedPatrons = this.InvitedPatrons |> List.filter (fun (u, _) -> u <> user) }
        }

    member this.DemotePatron(user: UserId) =
        result {
            do!
                this.Patrons
                |> List.exists (fun (u, _) -> u = user)
                |> Result.ofBool "User is not a patron"

            return
                { this with
                    Patrons =
                        this.Patrons
                        |> List.map (fun (u, r) -> if u = user then (u, PatronRole.User) else (u, r)) }
        }

    member this.PromotePatron(user: UserId) =
        result {
            do!
                this.Patrons
                |> List.exists (fun (u, _) -> u = user)
                |> Result.ofBool "User is not a patron"

            return
                { this with
                    Patrons =
                        this.Patrons
                        |> List.map (fun (u, r) -> if u = user then (u, PatronRole.Manager) else (u, r)) }
        }
    
    member this.SuspendPatron(user: UserId, reason: string) =
        result {
            do!
                this.Patrons
                |> List.exists (fun (u, _) -> u = user)
                |> Result.ofBool "User is not a patron"

            return
                { this with
                    Patrons =
                        this.Patrons
                        |> List.map (fun (u, r) -> if u = user then (u, PatronRole.Suspended reason) else (u, r)) }
        }

    member this.ReAdmittPatron (user: UserId) =
        result {
            do!
                this.Patrons
                |> List.exists (fun (u, _) -> u = user)
                |> Result.ofBool "User is not a patron"

            do! 
                this.Patrons
                |> List.find (fun (u, _) -> u = user)
                |> fun (_, r) -> r.IsSuspended
                |> Result.ofBool "Must be suspended before readmission"

            return
                { this with
                    Patrons =
                        this.Patrons
                        |> List.map (fun (u, r) -> if u = user then (u, PatronRole.User) else (u, r)) }
        }

    member this.RemovePatron(user: UserId) =
        result {
            do!
                this.Patrons
                |> List.exists (fun (u, _) -> u = user)
                |> Result.ofBool "User is not a patron"

            do! this.Patrons
                |> List.find (fun (u, r) -> u = user)
                |> fun (_, r) -> r.IsSuspended
                |> Result.ofBool "Must be suspended before removal"

            return
                { this with
                    Patrons = this.Patrons |> List.filter (fun (u, _) -> u <> user) }
        }

    member this.ReplaceTag(oldTag: Tag, newTag: Tag) =
        result {
            do!
                this.Tags
                |> List.exists (fun t -> t = oldTag)
                |> Result.ofBool "Tag does not exist"

            return
                { this with
                    Tags = this.Tags |> List.map (fun t -> if t = oldTag then newTag else t) }
        }

    member this.SetPublic() = { this with Public = true } |> Ok
    member this.SetPrivate() = { this with Public = false } |> Ok

    member this.GetUserRole userId =
        this.Patrons |> List.tryFind (fun (u, _) -> u = userId) |> Option.map snd

    member this.Activate =
        match this.TenantState with
        | Deactivated ->
            { this with
                TenantState = TenantState.Active }
            |> Ok
        | _ -> Error "Tenant is already activated"

    member this.ScheduleForDeletion(date: System.DateTime) =
        match this.TenantState with
        | Deactivated ->
            { this with
                TenantState = TenantState.ScheduledForDeletion date }
            |> Ok
        | _ -> Error "Tenant must be deactivated to be scheduled for deletion"

    member this.Id = this.TenantId.Value
    static member SnapshotsInterval = 100
    static member StorageName = "_Tenant"
    static member Version = "_01"

    member this.Serialize = (this, jsonOptions) |> JsonSerializer.Serialize

    static member Deserialize(data: string) =
        try
            (data, jsonOptions) |> JsonSerializer.Deserialize<Tenant> |> Ok
        with ex ->
            try
                let fallback = (data, jsonOptions) |> JsonSerializer.Deserialize<Tenant002>
                fallback.Upcast() |> Ok
            with _ ->
                try
                    let fallback1 = (data, jsonOptions) |> JsonSerializer.Deserialize<Tenant001>
                    fallback1.Upcast() |> Ok
                with _ ->
                    Error(ex.Message)
