
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
    testList "users tests" [
        testCaseTask "can add a user and retrieve it by a user context - Ok" <| fun _ -> task {
            setUp()
            let userService = getUserService()
            let! adminId = registerUserWithAdminRoleTask "admin@example.com" "Password123!"
            let adminContext = UserContext.Authenticated(adminId, [Role.Admin], TenantId.Default)
            let! userId = registerUserTask "user@example.com" "Password123!"
            let! user = userService.GetUserAsync(adminContext, userId) 
            Expect.isOk user "should be ok"
        }
        testCaseTask "a user can retrieve their own details - Ok" <| fun _ -> task {
            setUp()
            let userService = getUserService()
            let! userId = registerUserTask "user@example.com" "Password123!"
            let userContext = UserContext.Authenticated(userId, [], TenantId.Default) 
            let! user = userService.GetUserAsync(userContext, userId) 
            Expect.isOk user "should be ok"
        }
        testCaseTask "cannot retrieve a user from the context of a different ordinary user - Error" <| fun _ -> task {
            setUp()
            let userService = getUserService()
            let! userId1 = registerUserTask "user1@example.com" "Password123!"
            let! userId2 = registerUserTask "user2@example.com" "Password123!"
            let userContext2 = UserContext.Authenticated(userId2, [], TenantId.Default) 
            let! result = userService.GetUserAsync(userContext2, userId1) 
            Expect.isError result "should be error"
        }

        ftestCaseTask "create a user and retrieve it then change the current tenant of that user, then retrieve again" <| fun _ -> task {
            setUp()
            let userService = getUserService()
            let! userId = registerUserTask "user@example.com" "Password123!"
            
            let! result1 = userViewerAsync None userId.Value
            Expect.isOk result1 "should be ok"
            let (eventId1, _) = result1 |> Result.get
            printfn "XXXXX. event Id1: %i\n" eventId1
            let tenantService = getTenantService()
            let userContext = UserContext.Authenticated(userId, [], TenantId.Default) 
            let tenant = Tenant.New(userId, TenantName.New "Random Tenant" |> Result.get, "Addr")
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
    ]