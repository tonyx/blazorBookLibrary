module TenantTests

open TestSetup
open Expecto
open BookLibrary.Domain
open BookLibrary.Shared.Details
open System
open BookLibrary.Shared.Commons
open System.Threading
open BookLibrary.Details.Details
open BookLibrary.Shared.Services
open blazorBookLibrary.Shared.Infrastructure.Services
open System.Threading.Tasks

type TestMailNotificator() =
    let sentEmails = System.Collections.Concurrent.ConcurrentBag<string * string * string * string * string>()
    member _.SentEmails = sentEmails |> Seq.toList
    interface IMailNotificator with
        member _.SendEmailAsync(emailFrom, nameFrom, emailRecipient, subject, body) =
            sentEmails.Add(emailFrom, nameFrom, emailRecipient, subject, body)
            Task.CompletedTask

type TestNotificationService() =
    let notifications = System.Collections.Concurrent.ConcurrentBag<Notification>()
    member _.Notifications = notifications |> Seq.toList
    interface INotificationService with
        member _.CreateNotificationAsync(context, notification, ?ct) =
            notifications.Add(notification)
            Task.FromResult(Ok ())
        member _.GetUnreadNotificationsForUserAsync(context, ?ct) =
            Task.FromResult(Ok [])
        member _.GetAllNotificationsForUserAsync(context, ?ct) =
            Task.FromResult(Ok [])
        member _.MarkAsReadAsync(context, notificationId, ?ct) =
            Task.FromResult(Ok ())


[<Tests>]
let tests =
    testList
        "tenant tests"
        [ testCaseTask "an ordinary user can create a tenant as owner"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()

                  let! userId = registerUserTask "owner@test.com" "Password123!"
                  let userContext = UserContext.Authenticated(userId, [])

                  let tenant =
                      Tenant.New(userId, TenantName.New "My Personal Library" |> Result.get, "123 Main St")

                  let! result = tenantService.CreateTenantAsync(userContext, tenant)

                  Expect.isOk result "User should be able to create their own tenant"
              }
          testCaseTask "a user cannot create a tenant for someone else"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()

                  let! userId1 = registerUserTask "user1@test.com" "Password123!"
                  let! userId2 = registerUserTask "user2@test.com" "Password123!"

                  let user1Context = UserContext.Authenticated(userId1, [])

                  // User 1 tries to create a tenant for User 2
                  let tenant =
                      Tenant.New(userId2, TenantName.New "Stolen Library" |> Result.get, "123 Main St")

                  let! result = tenantService.CreateTenantAsync(user1Context, tenant)

                  Expect.isError result "User should not be able to create a tenant for another user"
              }
          testCaseTask "a user can retrieve a public tenant created by another user"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()

                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  // Create a public tenant (true is default)
                  let tenant =
                      Tenant.New(ownerId, TenantName.New "Public Library" |> Result.get, "123 Main St", true)

                  let tenantId = tenant.TenantId
                  let! createResult = tenantService.CreateTenantAsync(ownerContext, tenant)
                  Expect.isOk createResult "Tenant creation should succeed"

                  let! approveResult = tenantService.SetPublicAsync(adminContext, tenantId)
                  Expect.isOk approveResult "Admin approval should succeed"

                  let! otherUserId = registerUserTask "other@test.com" "Password123!"
                  let otherUserContext = UserContext.Authenticated(otherUserId, [])

                  // Other user retrieves the tenant
                  let! getResult = tenantService.GetTenantAsync(otherUserContext, tenantId)

                  Expect.isOk getResult "Other user should be able to retrieve the public tenant"

                  match getResult with
                  | Ok retrievedTenant ->
                      Expect.equal retrievedTenant.TenantId tenantId "Retrieved tenant should have correct ID"
                      Expect.isTrue retrievedTenant.TenantVisibility.IsPublic "Retrieved tenant should be public"
                  | Error msg -> failwith msg
              }
          testCaseTask "a user can retrieve their own private tenant"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()

                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  // Create a private tenant
                  let tenant =
                      Tenant.New(ownerId, TenantName.New "Private Library" |> Result.get, "123 Main St", false)

                  let tenantId = tenant.TenantId
                  let! createResult = tenantService.CreateTenantAsync(ownerContext, tenant)
                  Expect.isOk createResult "Tenant creation should succeed"

                  // Owner retrieves their own private tenant
                  let! getResult = tenantService.GetTenantAsync(ownerContext, tenantId)

                  Expect.isOk getResult "Owner should be able to retrieve their own private tenant"

                  match getResult with
                  | Ok retrievedTenant ->
                      Expect.equal retrievedTenant.TenantId tenantId "Retrieved tenant should have correct ID"
                      Expect.isFalse retrievedTenant.TenantVisibility.IsPublic "Retrieved tenant should be private"
                  | Error msg -> failwith msg
              }
          testCaseTask "an admin can retrieve a private tenant created by another user"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()

                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  // Create a private tenant
                  let tenant =
                      Tenant.New(ownerId, TenantName.New "Secret Library" |> Result.get, "123 Main St", false)

                  let tenantId = tenant.TenantId
                  let! createResult = tenantService.CreateTenantAsync(ownerContext, tenant)
                  Expect.isOk createResult "Tenant creation should succeed"

                  // Admin retrieves the private tenant
                  let! getResult = tenantService.GetTenantAsync(adminContext, tenantId)

                  Expect.isOk getResult "Admin should be able to retrieve any private tenant"

                  match getResult with
                  | Ok retrievedTenant ->
                      Expect.equal retrievedTenant.TenantId tenantId "Retrieved tenant should have correct ID"
                  | Error msg -> failwith msg
              }
          testCaseTask "a non-admin user cannot retrieve another user's private tenant"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()

                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  // Create a private tenant
                  let tenant =
                      Tenant.New(ownerId, TenantName.New "Private Library" |> Result.get, "123 Main St", false)

                  let tenantId = tenant.TenantId
                  let! createResult = tenantService.CreateTenantAsync(ownerContext, tenant)
                  Expect.isOk createResult "Tenant creation should succeed"

                  let! otherUserId = registerUserTask "other@test.com" "Password123!"
                  let otherUserContext = UserContext.Authenticated(otherUserId, [])

                  // Other user tries to retrieve the private tenant
                  let! getResult = tenantService.GetTenantAsync(otherUserContext, tenantId)

                  Expect.isError getResult "Non-admin user should not be able to retrieve private tenant"
              }
          testCaseTask "owner can add a patron to their tenant"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()
                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "My Library" |> Result.get, "123 Main St")

                  let tenantId = tenant.TenantId
                  let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)

                  let! patronId = registerUserTask "patron@test.com" "Password123!"
                  let! result = tenantService.AddPatronAsync(ownerContext, tenantId, patronId, PatronRole.User)

                  Expect.isOk result "Owner should be able to add a patron"

                  let! getResult = tenantService.GetTenantAsync(ownerContext, tenantId)

                  match getResult with
                  | Ok t ->
                      Expect.exists
                          t.Patrons
                          (fun (u, r) -> u = patronId && r = PatronRole.User)
                          "Patron should be in the list"
                  | Error msg -> failwith msg
              }
          testCaseTask "owner can promote and demote a patron"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()
                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "My Library" |> Result.get, "123 Main St")

                  let tenantId = tenant.TenantId
                  let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)

                  let! patronId = registerUserTask "patron@test.com" "Password123!"
                  let! _ = tenantService.AddPatronAsync(ownerContext, tenantId, patronId, PatronRole.User)

                  let! promoteResult = tenantService.PromotePatronAsync(ownerContext, tenantId, patronId)
                  Expect.isOk promoteResult "Owner should be able to promote a patron"

                  let! getResult1 = tenantService.GetTenantAsync(ownerContext, tenantId)

                  match getResult1 with
                  | Ok t ->
                      Expect.exists
                          t.Patrons
                          (fun (u, r) -> u = patronId && r = PatronRole.Manager)
                          "Patron should be a manager"
                  | Error msg -> failwith msg

                  let! demoteResult = tenantService.DemotePatronAsync(ownerContext, tenantId, patronId)
                  Expect.isOk demoteResult "Owner should be able to demote a patron"

                  let! getResult2 = tenantService.GetTenantAsync(ownerContext, tenantId)

                  match getResult2 with
                  | Ok t ->
                      Expect.exists
                          t.Patrons
                          (fun (u, r) -> u = patronId && r = PatronRole.User)
                          "Patron should be a user again"
                  | Error msg -> failwith msg
              }
          testCaseTask "owner can remove a patron"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()
                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "My Library" |> Result.get, "123 Main St")

                  let tenantId = tenant.TenantId
                  let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)

                  let! patronId = registerUserTask "patron@test.com" "Password123!"
                  let! _ = tenantService.AddPatronAsync(ownerContext, tenantId, patronId, PatronRole.User)

                  let! suspendResult =
                      tenantService.SuspendPatron(ownerContext, tenantId, patronId, "Suspended for removal")

                  Expect.isOk suspendResult "Owner should be able to suspend a patron"

                  let! removeResult = tenantService.RemovePatronAsync(ownerContext, tenantId, patronId)
                  Expect.isOk removeResult "Owner should be able to remove a patron"

                  let! getResult = tenantService.GetTenantAsync(ownerContext, tenantId)

                  match getResult with
                  | Ok t ->
                      Expect.isFalse (t.Patrons |> List.exists (fun (u, _) -> u = patronId)) "Patron should be removed"
                  | Error msg -> failwith msg
              }
          testCaseTask "non-owner cannot add a patron"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()
                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "My Library" |> Result.get, "123 Main St")

                  let tenantId = tenant.TenantId
                  let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)

                  let! otherUserId = registerUserTask "other@test.com" "Password123!"
                  let otherContext = UserContext.Authenticated(otherUserId, [])

                  let! patronId = registerUserTask "patron@test.com" "Password123!"
                  let! result = tenantService.AddPatronAsync(otherContext, tenantId, patronId, PatronRole.User)

                  Expect.isError result "Non-owner should not be able to add a patron"
              }
          testCaseTask "a patron can retrieve their own role"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()
                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "My Library" |> Result.get, "123 Main St", false)

                  let tenantId = tenant.TenantId
                  let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)

                  let! patronId = registerUserTask "patron@test.com" "Password123!"
                  let! _ = tenantService.AddPatronAsync(ownerContext, tenantId, patronId, PatronRole.User)

                  let patronContext = UserContext.Authenticated(patronId, []).WithNewTenant(tenantId)
                  let! roleResult = tenantService.GetUserRoleAsync(patronContext, tenantId, patronId)

                  Expect.isOk roleResult "Patron should be able to retrieve their own role"

                  match roleResult with
                  | Ok role -> Expect.equal role PatronRole.User "Role should be correct"
                  | Error msg -> failwith msg
              }
          testCaseTask "a patron can retrieve a private tenant they belong to"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()
                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "Private Library" |> Result.get, "123 Main St", false)

                  let tenantId = tenant.TenantId
                  let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)

                  let! patronId = registerUserTask "patron@test.com" "Password123!"
                  let! _ = tenantService.AddPatronAsync(ownerContext, tenantId, patronId, PatronRole.User)

                  let patronContext = UserContext.Authenticated(patronId, []).WithNewTenant(tenantId)
                  let! getResult = tenantService.GetTenantAsync(patronContext, tenantId)

                  Expect.isOk getResult "Patron should be able to retrieve a private tenant they belong to"
              }
          testCaseTask "owner can invite a patron and convert invited patron to patron"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()
                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "My Library" |> Result.get, "123 Main St")

                  let tenantId = tenant.TenantId
                  let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)

                  let! patronId = registerUserTask "patron@test.com" "Password123!"
                  let! inviteResult = tenantService.InvitePatronAsync(ownerContext, tenantId, patronId)
                  Expect.isOk inviteResult "Owner should be able to invite a patron"

                  let! getResult = tenantService.GetTenantAsync(ownerContext, tenantId)

                  match getResult with
                  | Ok t ->
                      Expect.equal t.InvitedPatrons.Length 1 "There should be one invited patron"
                      let (_, code) = t.InvitedPatrons.[0]
                      let! convertResult = tenantService.ConvertInvitedPatronToPatronAsync(ownerContext, tenantId, code)
                      Expect.isOk convertResult "Owner should be able to convert invited patron to patron"

                      let! getResultAfter = tenantService.GetTenantAsync(ownerContext, tenantId)

                      match getResultAfter with
                      | Ok tAfter ->
                          Expect.equal tAfter.InvitedPatrons.Length 0 "Invited patrons should be empty"

                          Expect.exists
                              tAfter.Patrons
                              (fun (u, r) -> u = patronId && r = PatronRole.User)
                              "Patron should be in the list"
                      | Error msg -> failwith msg
                  | Error msg -> failwith msg
              }
          testCaseTask "owner can suspend and readmit a patron"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()
                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "My Library" |> Result.get, "123 Main St")

                  let tenantId = tenant.TenantId
                  let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)

                  let! patronId = registerUserTask "patron@test.com" "Password123!"
                  let! _ = tenantService.AddPatronAsync(ownerContext, tenantId, patronId, PatronRole.User)

                  let! suspendResult =
                      tenantService.SuspendPatron(ownerContext, tenantId, patronId, "Violated policies")

                  Expect.isOk suspendResult "Owner should be able to suspend a patron"

                  let! getResult = tenantService.GetTenantAsync(ownerContext, tenantId)

                  match getResult with
                  | Ok t ->
                      Expect.exists
                          t.Patrons
                          (fun (u, r) -> u = patronId && r = PatronRole.Suspended "Violated policies")
                          "Patron should be suspended"
                  | Error msg -> failwith msg

                  let! readmitResult = tenantService.ReAdmittPatron(ownerContext, tenantId, patronId)
                  Expect.isOk readmitResult "Owner should be able to readmit a patron"

                  let! getResult2 = tenantService.GetTenantAsync(ownerContext, tenantId)

                  match getResult2 with
                  | Ok t ->
                      Expect.exists
                          t.Patrons
                          (fun (u, r) -> u = patronId && r = PatronRole.User)
                          "Patron should be readmitted to User role"
                  | Error msg -> failwith msg
              }
          testCaseTask "non-owner/non-admin cannot suspend or readmit a patron"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()
                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "My Library" |> Result.get, "123 Main St")

                  let tenantId = tenant.TenantId
                  let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)

                  let! patronId = registerUserTask "patron@test.com" "Password123!"
                  let! _ = tenantService.AddPatronAsync(ownerContext, tenantId, patronId, PatronRole.User)

                  let! otherUserId = registerUserTask "other@test.com" "Password123!"
                  let otherContext = UserContext.Authenticated(otherUserId, [])

                  let! suspendResult = tenantService.SuspendPatron(otherContext, tenantId, patronId, "Some reason")
                  Expect.isError suspendResult "Non-owner should not be able to suspend a patron"

                  // Suspend first as owner so we can try to readmit as non-owner
                  let! _ = tenantService.SuspendPatron(ownerContext, tenantId, patronId, "Some reason")

                  let! readmitResult = tenantService.ReAdmittPatron(otherContext, tenantId, patronId)
                  Expect.isError readmitResult "Non-owner should not be able to readmit a patron"
              }
          testCaseTask "owner can generate a join PIN and find tenant by PIN"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()
                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "Library with PIN" |> Result.get, "123 Main St")

                  let tenantId = tenant.TenantId
                  let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)

                  let pin = "123456"
                  let! genPinResult = tenantService.GenerateJoinPinAsync(ownerContext, tenantId, pin)
                  Expect.isOk genPinResult "Owner should be able to generate a join PIN"

                  let! findResult = tenantService.FindTenantByJoinPinAsync(pin)
                  Expect.isOk findResult "Should be able to find the tenant by PIN"

                  match findResult with
                  | Ok foundTenant ->
                      Expect.equal foundTenant.TenantId tenantId "Found tenant should have the correct ID"
                      Expect.equal foundTenant.CurrentJoinPin (Some pin) "Found tenant should have the correct PIN"
                  | Error msg -> failwith msg
              }
          testCaseTask "owner can add and remove tenant tags"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()
                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "Library with tags" |> Result.get, "123 Main St")

                  let tenantId = tenant.TenantId
                  let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)

                  let tag = Tag.BookTag "Science"
                  let! addResult = tenantService.AddTagAsync(ownerContext, tenantId, tag)
                  Expect.isOk addResult "Owner should be able to add a tag to the tenant"

                  let! getResult = tenantService.GetTenantAsync(ownerContext, tenantId)
                  Expect.isOk getResult "Should retrieve tenant"
                  match getResult with
                  | Ok t ->
                      Expect.contains t.Tags tag "Tenant should contain the added tag"
                  | Error msg -> failwith msg

                  let! removeResult = tenantService.RemoveTagAsync(ownerContext, tenantId, tag)
                  Expect.isOk removeResult "Owner should be able to remove a tag from the tenant"

                  let! getResult2 = tenantService.GetTenantAsync(ownerContext, tenantId)
                  Expect.isOk getResult2 "Should retrieve tenant"
                  match getResult2 with
                  | Ok t ->
                      Expect.isFalse (t.Tags |> List.contains tag) "Tenant should not contain the removed tag"
                  | Error msg -> failwith msg
              }
          testCaseTask "owner can set and disable geoloc settings on tenant"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()
                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "Library with geoloc" |> Result.get, "123 Main St")

                  let tenantId = tenant.TenantId
                  let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)

                  let! otherUserId = registerUserTask "other@test.com" "Password123!"
                  let otherUserContext = UserContext.Authenticated(otherUserId, [])

                  let geoLoc = GeoLocSettings.CreateEnabled(45.4642, 9.1900) |> Result.get

                  // Other user cannot set geoloc
                  let! unauthorizedSetResult = tenantService.SetGeoLocSettingsAsync(otherUserContext, tenantId, geoLoc)
                  Expect.isError unauthorizedSetResult "Non-manager should not be able to set geoloc settings"

                  // Owner sets geoloc
                  let! setResult = tenantService.SetGeoLocSettingsAsync(ownerContext, tenantId, geoLoc)
                  Expect.isOk setResult "Owner should be able to set geoloc settings"

                  let! getResult = tenantService.GetTenantAsync(ownerContext, tenantId)
                  Expect.isOk getResult "Should retrieve tenant"
                  match getResult with
                  | Ok t ->
                      Expect.equal t.GeoLocSettings geoLoc "Tenant should have the configured geoloc settings"
                  | Error msg -> failwith msg

                  // Other user cannot disable geoloc
                  let! unauthorizedDisableResult = tenantService.DisableGeoLocSettingsAsync(otherUserContext, tenantId)
                  Expect.isError unauthorizedDisableResult "Non-manager should not be able to disable geoloc settings"

                  // Owner disables geoloc
                  let! disableResult = tenantService.DisableGeoLocSettingsAsync(ownerContext, tenantId)
                  Expect.isOk disableResult "Owner should be able to disable geoloc settings"

                  let! getResult2 = tenantService.GetTenantAsync(ownerContext, tenantId)
                  Expect.isOk getResult2 "Should retrieve tenant"
                  match getResult2 with
                  | Ok t ->
                      Expect.equal t.GeoLocSettings GeoLocSettings.Disabled "Tenant should have disabled geoloc settings"
                  | Error msg -> failwith msg
              }
          testCaseTask "retrieve public tenants by proximity"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()

                  let! ownerId = registerUserTask "owner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "Milan Public Library" |> Result.get, "Via Dante 1", true)

                  let tenantId = tenant.TenantId
                  let! createResult = tenantService.CreateTenantAsync(ownerContext, tenant)
                  Expect.isOk createResult "Tenant creation should succeed"

                  let! approveResult = tenantService.SetPublicAsync(adminContext, tenantId)
                  Expect.isOk approveResult "Admin approval should succeed"

                  let geoLoc = GeoLocSettings.CreateEnabled(45.4642, 9.1900) |> Result.get
                  let! setResult = tenantService.SetGeoLocSettingsAsync(ownerContext, tenantId, geoLoc)
                  Expect.isOk setResult "Setting geoloc should succeed"

                  // User in Milan (within 1000m)
                  let milanUserCoords = Coordinates.New(45.4645, 9.1905, 1000.0)
                  let romeUserCoords = Coordinates.New(41.9028, 12.4964, 1000.0)
                  let! nearResult = tenantService.GetAllPublicTenantsByProximityAsync(UserContext.Anonymous, milanUserCoords)
                  Expect.isOk nearResult "Should succeed retrieving tenants by proximity"
                  match nearResult with
                  | Ok tenants ->
                      Expect.isTrue (tenants |> List.exists (fun t -> t.TenantId = tenantId)) "Milan library should be found by nearby user"
                  | Error msg -> failwith msg

                  // Test GetAllowedTenantsByProximityAsync
                  // Anonymous nearby: found
                  let! allowedNearResult = tenantService.GetAllowedTenantsByProximityAsync(UserContext.Anonymous, milanUserCoords)
                  Expect.isOk allowedNearResult "Should succeed retrieving allowed tenants by proximity"
                  match allowedNearResult with
                  | Ok tenants ->
                      Expect.isTrue (tenants |> List.exists (fun t -> t.TenantId = tenantId)) "Milan library should be in allowed tenants for nearby anonymous user"
                  | Error msg -> failwith msg

                  // Anonymous far away: NOT found
                  let! allowedFarResult = tenantService.GetAllowedTenantsByProximityAsync(UserContext.Anonymous, romeUserCoords)
                  Expect.isOk allowedFarResult "Should succeed retrieving allowed tenants by proximity"
                  match allowedFarResult with
                  | Ok tenants ->
                      Expect.isFalse (tenants |> List.exists (fun t -> t.TenantId = tenantId)) "Milan library should NOT be in allowed tenants for distant anonymous user"
                      Expect.isTrue (tenants |> List.exists (fun t -> t.TenantId = TenantId.Default)) "Default tenant should always be included for anonymous user"
                  | Error msg -> failwith msg

                  // Owner far away: FOUND because user is owner
                  let! allowedOwnerFarResult = tenantService.GetAllowedTenantsByProximityAsync(ownerContext, romeUserCoords)
                  Expect.isOk allowedOwnerFarResult "Should succeed retrieving allowed tenants for owner"
                  match allowedOwnerFarResult with
                  | Ok tenants ->
                      Expect.isTrue (tenants |> List.exists (fun t -> t.TenantId = tenantId)) "Milan library should be in allowed tenants for distant owner"
                  | Error msg -> failwith msg
              }
          testCaseTask "partner API key lifecycle: add, validate, duplicate rejection, list, and revoke"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()
                  let tenantApiKeyService = getTenantApiKeyService ()

                  let! ownerId = registerUserTask "keyowner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let! partnerId = registerUserTask "partnerdev@test.com" "Password123!"

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "API Key Enabled Tenant" |> Result.get, "Via Roma 10", true)
                  let tenantId = tenant.TenantId
                  let! createResult = tenantService.CreateTenantAsync(ownerContext, tenant)
                  Expect.isOk createResult "Tenant creation should succeed"

                  // 1. Add partner key
                  let key = CatalogApiKey.New()
                  let partnerKey: PartnerApiKey =
                      { Key = key
                        PartnerId = partnerId
                        Label = "University OPAC"
                        CreatedAt = DateTime.UtcNow
                        RevokedAt = None
                        ExpiresAt = None }

                  let! addResult = tenantApiKeyService.AddPartnerKeyAsync(ownerContext, tenantId, partnerKey)
                  Expect.isOk addResult "Adding partner key by owner should succeed"

                  // 2. Validate key
                  let! validResult = tenantApiKeyService.ValidateCatalogKeyAsync(tenantId, key)
                  Expect.isOk validResult "Key validation should succeed for active key"

                  // 3. Reject duplicate key for same partner user
                  let key2 = CatalogApiKey.New()
                  let duplicatePartnerKey: PartnerApiKey =
                      { Key = key2
                        PartnerId = partnerId
                        Label = "Duplicate Attempt"
                        CreatedAt = DateTime.UtcNow
                        RevokedAt = None
                        ExpiresAt = None }
                  let! duplicateResult = tenantApiKeyService.AddPartnerKeyAsync(ownerContext, tenantId, duplicatePartnerKey)
                  Expect.isError duplicateResult "Adding duplicate key for same partner should fail"

                  // 4. List keys
                  let! listResult = tenantApiKeyService.GetPartnerKeysAsync(ownerContext, tenantId)
                  Expect.isOk listResult "Owner should be able to list partner keys"
                  match listResult with
                  | Ok keys ->
                      Expect.equal keys.Length 1 "Should have 1 partner key"
                      Expect.equal keys.[0].Key key "Listed key should match added key"
                      Expect.equal keys.[0].Label "University OPAC" "Label should match"
                  | Error msg -> failwith msg

                  // 5. Revoke key
                  let! revokeResult = tenantApiKeyService.RevokeCatalogKeyAsync(ownerContext, tenantId, key)
                  Expect.isOk revokeResult "Revoking key should succeed"

                  // 6. Verify revocation reflected in list
                  let! listAfterRevoke = tenantApiKeyService.GetPartnerKeysAsync(ownerContext, tenantId)
                  Expect.isOk listAfterRevoke "Listing after revoke should succeed"
                  match listAfterRevoke with
                  | Ok keys ->
                      Expect.isTrue keys.[0].RevokedAt.IsSome "RevokedAt should be Some"
                  | Error msg -> failwith msg
              }
          testCaseTask "a tenant owner can add partner key using overloaded AddPartnerKeyAsync"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()
                  let tenantApiKeyService = getTenantApiKeyService ()

                  let! ownerId = registerUserTask "keyowner_overload@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])
                  let! partnerId = registerUserTask "partnerdev_overload@test.com" "Password123!"

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "API Key Overload Tenant" |> Result.get, "Via Roma 11", true)
                  let tenantId = tenant.TenantId
                  let! createResult = tenantService.CreateTenantAsync(ownerContext, tenant)
                  Expect.isOk createResult "Tenant creation should succeed"

                  let now = DateTime.UtcNow
                  let expiresAt = now.AddDays(30.0)
                  let! addResult = tenantApiKeyService.AddPartnerKeyAsync(ownerContext, tenantId, partnerId, "Library Partner", now, expiresAt = expiresAt)
                  Expect.isOk addResult "Overloaded AddPartnerKeyAsync should succeed"

                  let! listResult = tenantApiKeyService.GetPartnerKeysAsync(ownerContext, tenantId)
                  Expect.isOk listResult "Owner should be able to list partner keys"
                  match listResult with
                  | Ok keys ->
                      Expect.equal keys.Length 1 "Should have 1 partner key"
                      Expect.equal keys.[0].PartnerId partnerId "PartnerId should match"
                      Expect.equal keys.[0].Label "Library Partner" "Label should match"
                      Expect.equal keys.[0].ExpiresAt (Some expiresAt) "ExpiresAt should match"
                      let! validResult = tenantApiKeyService.ValidateCatalogKeyAsync(tenantId, keys.[0].Key)
                      Expect.isOk validResult "Key validation should succeed"
                  | Error msg -> failwith msg
              }
          testCaseTask "a global admin or manager can permanently remove a partner API key using RemovePartnerKeyAsync"
          <| fun _ ->
              task {
                  setUp ()
                  let tenantService = getTenantService ()
                  let tenantApiKeyService = getTenantApiKeyService ()

                  let! ownerId = registerUserTask "removekeyowner@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])
                  let! partnerId = registerUserTask "removekeypartner@test.com" "Password123!"

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "API Key Removal Tenant" |> Result.get, "Via Roma 12", true)
                  let tenantId = tenant.TenantId
                  let! createResult = tenantService.CreateTenantAsync(ownerContext, tenant)
                  Expect.isOk createResult "Tenant creation should succeed"

                  let key = CatalogApiKey.New()
                  let partnerKey: PartnerApiKey =
                      { Key = key
                        PartnerId = partnerId
                        Label = "Temporary Key"
                        CreatedAt = DateTime.UtcNow
                        RevokedAt = None
                        ExpiresAt = None }

                  let! addResult = tenantApiKeyService.AddPartnerKeyAsync(ownerContext, tenantId, partnerKey)
                  Expect.isOk addResult "Adding key should succeed"

                  // Permanently remove the key
                  let! removeResult = tenantApiKeyService.RemovePartnerKeyAsync(ownerContext, tenantId, key)
                  Expect.isOk removeResult "Removing partner key should succeed"

                  // Key must no longer be in the list
                  let! listResult = tenantApiKeyService.GetPartnerKeysAsync(ownerContext, tenantId)
                  Expect.isOk listResult "Getting partner keys should succeed"
                  match listResult with
                  | Ok keys -> Expect.isTrue (keys |> List.isEmpty) "Partner keys list should be empty after removal"
                  | Error msg -> failwith msg

                  // Validating the removed key must fail
                  let! validateResult = tenantApiKeyService.ValidateCatalogKeyAsync(tenantId, key)
                  Expect.isError validateResult "Validation must fail for removed key"
              }
          testCaseTask "revoking a manager's partner API key notifies them via email and in-app notification"
          <| fun _ ->
              task {
                  setUp ()
                  let mailSpy = TestMailNotificator()
                  let notifSpy = TestNotificationService()
                  let tenantService = getTenantService ()
                  let tenantApiKeyService = getTenantApiKeyServiceWithMocks mailSpy notifSpy

                  let! ownerId = registerUserTask "keymanager_revoke@test.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "Manager Notified Tenant" |> Result.get, "Via Roma 13", true)
                  let tenantId = tenant.TenantId
                  let! createResult = tenantService.CreateTenantAsync(ownerContext, tenant)
                  Expect.isOk createResult "Tenant creation should succeed"

                  // Add partner key for owner (who is also manager/owner of the tenant)
                  let now = DateTime.UtcNow
                  let! addResult = tenantApiKeyService.AddPartnerKeyAsync(ownerContext, tenantId, ownerId, "Owner Secret Integration", now)
                  Expect.isOk addResult "Adding key for manager/owner should succeed"

                  let! listResult = tenantApiKeyService.GetPartnerKeysAsync(ownerContext, tenantId)
                  let key =
                      match listResult with
                      | Ok (k :: _) -> k.Key
                      | _ -> failwith "Expected at least one key"

                  // Admin revokes the manager's key
                  let! revokeResult = tenantApiKeyService.RevokeCatalogKeyAsync(adminContext, tenantId, key)
                  Expect.isOk revokeResult "Revocation by admin should succeed"

                  // Verify in-app notification sent
                  let notifs = notifSpy.Notifications
                  Expect.equal notifs.Length 1 "Should receive 1 in-app notification"
                  Expect.equal notifs.[0].UserId ownerId "Notification recipient must be the manager"
                  Expect.isTrue (notifs.[0].Title.Contains "API Key Revoked") "Title should mention API Key Revoked"
                  Expect.equal notifs.[0].ActionUrl (Some "/tenants") "ActionUrl should be /tenants"

                  // Verify email sent
                  let emails = mailSpy.SentEmails
                  Expect.equal emails.Length 1 "Should send 1 email"
                  let (_, _, recipientEmail, subject, body) = emails.[0]
                  Expect.isTrue (subject.Contains "API Key Revoked") "Email subject should mention API Key Revoked"
                  Expect.isTrue (body.Contains "Owner Secret Integration") "Email body should mention key label"
                  Expect.isTrue (body.Contains key.Value) "Email body should contain the revoked key"
              } ]


