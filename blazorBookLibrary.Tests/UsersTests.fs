module UsersTests

open System
open TestSetup
open Expecto
open BookLibrary.Domain
open BookLibrary.Shared.Details
open BookLibrary.Shared.Commons
open System.Threading
open BookLibrary.Details.Details
open BookLibrary.Shared.Services

[<Tests>]
let tests =
    testList
        "users tests"
        [ testCaseTask "can add a user and retrieve it by a user context - Ok"
          <| fun _ ->
              task {
                  setUp ()
                  let userService = getUserService ()
                  let! adminId = registerUserWithAdminRoleTask "admin@example.com" "Password123!"
                  let adminContext = UserContext.Authenticated(adminId, [ Role.Admin ])
                  let! userId = registerUserTask "user@example.com" "Password123!"
                  let! user = userService.GetUserAsync(adminContext, userId)
                  Expect.isOk user "should be ok"
              }
          testCaseTask "a user can retrieve their own details - Ok"
          <| fun _ ->
              task {
                  setUp ()
                  let userService = getUserService ()
                  let! userId = registerUserTask "user@example.com" "Password123!"
                  let userContext = UserContext.Authenticated(userId, [])
                  let! user = userService.GetUserAsync(userContext, userId)
                  Expect.isOk user "should be ok"
              }
          testCaseTask "cannot retrieve a user from the context of a different ordinary user - Error"
          <| fun _ ->
              task {
                  setUp ()
                  let userService = getUserService ()
                  let! userId1 = registerUserTask "user1@example.com" "Password123!"
                  let! userId2 = registerUserTask "user2@example.com" "Password123!"
                  let userContext2 = UserContext.Authenticated(userId2, [])
                  let! result = userService.GetUserAsync(userContext2, userId1)
                  Expect.isError result "should be error"
              }

          testCaseTask "create a user and retrieve it then change the current tenant of that user, then retrieve again"
          <| fun _ ->
              task {
                  setUp ()
                  let userService = getUserService ()
                  let! userId = registerUserTask "user@example.com" "Password123!"

                  let! result1 = userViewerAsync None userId.Value
                  Expect.isOk result1 "should be ok"
                  let (eventId1, _) = result1 |> Result.get
                  let tenantService = getTenantService ()
                  let userContext = UserContext.Authenticated(userId, [])

                  let tenant =
                      Tenant.New(userId, TenantName.New "Random Tenant" |> Result.get, "Addr")

                  let! createResult = tenantService.CreateTenantAsync(userContext, tenant)
                  Expect.isOk createResult (sprintf "tenant creation failed: %A" createResult)

                  let! setTenantResult = userService.SetCurrentTenantAsync(adminContext, userId, tenant.TenantId)
                  Expect.isOk setTenantResult "should be ok"

                  let! result2 = userViewerAsync None userId.Value
                  Expect.isOk result2 "should be ok"
                  let (eventId2, _) = result2 |> Result.get

                  Expect.isTrue (eventId2 > 0) "eventId should be greater than zero"
                  Expect.isTrue (eventId2 > eventId1) "eventId should have increased"
              }

          testCaseTask "create a public tenant and ensure another ordinary user can select it even if not entitled"
          <| fun _ ->
              task {
                  setUp ()
                  let userService = getUserService ()
                  let tenantService = getTenantService ()

                  let! ownerId = registerUserTask "owner@example.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "Public Tenant" |> Result.get, "Public Address", true)

                  let! createResult = tenantService.CreateTenantAsync(ownerContext, tenant)
                  Expect.isOk createResult (sprintf "tenant creation failed: %A" createResult)

                  let! approveResult = tenantService.SetPublicAsync(adminContext, tenant.TenantId)
                  Expect.isOk approveResult "Admin approval should succeed"

                  let! visitorId = registerUserTask "visitor@example.com" "Password123!"
                  let visitorContext = UserContext.Authenticated(visitorId, [])

                  let! setTenantResult = userService.SetCurrentTenantAsync(visitorContext, visitorId, tenant.TenantId)
                  Expect.isOk setTenantResult "visitor should be able to select the public tenant"

                  let! result = userViewerAsync None visitorId.Value
                  Expect.isOk result "should be ok"
                  let (_, user) = result |> Result.get

                  Expect.equal
                      user.CurrentTenant
                      tenant.TenantId
                      "Current tenant should match the selected public tenant"
              }
          testCaseTask "invited user to private tenant can set current tenant, resolve it, and see it in allowed tenants"
          <| fun _ ->
              task {
                  setUp ()
                  let userService = getUserService ()
                  let tenantService = getTenantService ()
                  let resolverService = getUserTenantResolverService ()

                  let! ownerId = registerUserTask "owner_priv@example.com" "Password123!"
                  let ownerContext = UserContext.Authenticated(ownerId, [])

                  let tenant =
                      Tenant.New(ownerId, TenantName.New "Private Tenant" |> Result.get, "Private Address", false)

                  let! createResult = tenantService.CreateTenantAsync(ownerContext, tenant)
                  Expect.isOk createResult (sprintf "tenant creation failed: %A" createResult)

                  let! inviteeId = registerUserTask "invitee@example.com" "Password123!"
                  let inviteeContext = UserContext.Authenticated(inviteeId, [])

                  // Before invitation, invitee cannot select private tenant
                  let! uninvitedSetResult = userService.SetCurrentTenantAsync(inviteeContext, inviteeId, tenant.TenantId)
                  Expect.isError uninvitedSetResult "uninvited user should not be able to select private tenant"

                  // Owner invites patron
                  let! inviteResult = tenantService.InvitePatronAsync(ownerContext, tenant.TenantId, inviteeId)
                  Expect.isOk inviteResult "owner should be able to invite patron"

                  // Invitee can get the private tenant
                  let! getTenantRes = tenantService.GetTenantAsync(inviteeContext, tenant.TenantId)
                  Expect.isOk getTenantRes "invitee should be able to get invited tenant"

                  // Invitee sees private tenant in allowed tenants
                  let! allowedTenantsRes = tenantService.GetAllowedTenantsAsync(inviteeContext)
                  Expect.isOk allowedTenantsRes "invitee should be able to get allowed tenants"
                  let allowed = allowedTenantsRes |> Result.get
                  Expect.isTrue (allowed |> List.exists (fun t -> t.TenantId = tenant.TenantId)) "allowed tenants should include invited tenant"

                  // Invitee can set current tenant to the invited tenant
                  let! setTenantResult = userService.SetCurrentTenantAsync(inviteeContext, inviteeId, tenant.TenantId)
                  Expect.isOk setTenantResult "invitee should be able to select invited private tenant"

                  // Resolver returns the invited tenant for the invitee
                  let! resolvedTenantRes = resolverService.GetTenantForUserAsync(inviteeContext)
                  Expect.isOk resolvedTenantRes "resolver should succeed"
                  Expect.equal (resolvedTenantRes |> Result.get) tenant.TenantId "resolver should resolve to invited tenant"
              } ]
