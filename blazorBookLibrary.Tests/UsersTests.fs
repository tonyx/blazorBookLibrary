
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
    ]