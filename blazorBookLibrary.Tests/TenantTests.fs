
module TenantTests

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
    testList "tenant tests" [
        testCaseTask "an ordinary user can create a tenant as owner" <| fun _ -> task {
            setUp()
            let tenantService = getTenantService()
            
            let! userId = registerUserTask "owner@test.com" "Password123!"
            let userContext = UserContext.Authenticated(userId, [], TenantId.Default)
            
            let tenant = Tenant.New(userId, TenantName.New "My Personal Library" |> Result.get, "123 Main St")
            let! result = tenantService.CreateTenantAsync(userContext, tenant)
            
            Expect.isOk result "User should be able to create their own tenant"
        }
        testCaseTask "a user cannot create a tenant for someone else" <| fun _ -> task {
            setUp()
            let tenantService = getTenantService()
            
            let! userId1 = registerUserTask "user1@test.com" "Password123!"
            let! userId2 = registerUserTask "user2@test.com" "Password123!"
            
            let user1Context = UserContext.Authenticated(userId1, [], TenantId.Default)
            
            // User 1 tries to create a tenant for User 2
            let tenant = Tenant.New(userId2, TenantName.New "Stolen Library" |> Result.get, "123 Main St")
            let! result = tenantService.CreateTenantAsync(user1Context, tenant)
            
            Expect.isError result "User should not be able to create a tenant for another user"
        }
        testCaseTask "a user can retrieve a public tenant created by another user" <| fun _ -> task {
            setUp()
            let tenantService = getTenantService()
            
            let! ownerId = registerUserTask "owner@test.com" "Password123!"
            let ownerContext = UserContext.Authenticated(ownerId, [], TenantId.Default)
            
            // Create a public tenant (true is default)
            let tenant = Tenant.New(ownerId, TenantName.New "Public Library" |> Result.get, "123 Main St", true)
            let tenantId = tenant.TenantId
            let! createResult = tenantService.CreateTenantAsync(ownerContext, tenant)
            Expect.isOk createResult "Tenant creation should succeed"
            
            let! otherUserId = registerUserTask "other@test.com" "Password123!"
            let otherUserContext = UserContext.Authenticated(otherUserId, [], TenantId.Default)
            
            // Other user retrieves the tenant
            let! getResult = tenantService.GetTenantAsync(otherUserContext, tenantId)
            
            Expect.isOk getResult "Other user should be able to retrieve the public tenant"
            match getResult with
            | Ok retrievedTenant ->
                Expect.equal retrievedTenant.TenantId tenantId "Retrieved tenant should have correct ID"
                Expect.isTrue retrievedTenant.Public "Retrieved tenant should be public"
            | Error msg -> failwith msg
        }
        testCaseTask "a user can retrieve their own private tenant" <| fun _ -> task {
            setUp()
            let tenantService = getTenantService()
            
            let! ownerId = registerUserTask "owner@test.com" "Password123!"
            let ownerContext = UserContext.Authenticated(ownerId, [], TenantId.Default)
            
            // Create a private tenant
            let tenant = Tenant.New(ownerId, TenantName.New "Private Library" |> Result.get, "123 Main St", false)
            let tenantId = tenant.TenantId
            let! createResult = tenantService.CreateTenantAsync(ownerContext, tenant)
            Expect.isOk createResult "Tenant creation should succeed"
            
            // Owner retrieves their own private tenant
            let! getResult = tenantService.GetTenantAsync(ownerContext, tenantId)
            
            Expect.isOk getResult "Owner should be able to retrieve their own private tenant"
            match getResult with
            | Ok retrievedTenant ->
                Expect.equal retrievedTenant.TenantId tenantId "Retrieved tenant should have correct ID"
                Expect.isFalse retrievedTenant.Public "Retrieved tenant should be private"
            | Error msg -> failwith msg
        }
        testCaseTask "an admin can retrieve a private tenant created by another user" <| fun _ -> task {
            setUp()
            let tenantService = getTenantService()
            
            let! ownerId = registerUserTask "owner@test.com" "Password123!"
            let ownerContext = UserContext.Authenticated(ownerId, [], TenantId.Default)
            
            // Create a private tenant
            let tenant = Tenant.New(ownerId, TenantName.New "Secret Library" |> Result.get, "123 Main St", false)
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
        testCaseTask "a non-admin user cannot retrieve another user's private tenant" <| fun _ -> task {
            setUp()
            let tenantService = getTenantService()
            
            let! ownerId = registerUserTask "owner@test.com" "Password123!"
            let ownerContext = UserContext.Authenticated(ownerId, [], TenantId.Default)
            
            // Create a private tenant
            let tenant = Tenant.New(ownerId, TenantName.New "Private Library" |> Result.get, "123 Main St", false)
            let tenantId = tenant.TenantId
            let! createResult = tenantService.CreateTenantAsync(ownerContext, tenant)
            Expect.isOk createResult "Tenant creation should succeed"
            
            let! otherUserId = registerUserTask "other@test.com" "Password123!"
            let otherUserContext = UserContext.Authenticated(otherUserId, [], TenantId.Default)
            
            // Other user tries to retrieve the private tenant
            let! getResult = tenantService.GetTenantAsync(otherUserContext, tenantId)
            
            Expect.isError getResult "Non-admin user should not be able to retrieve private tenant"
        }
    ]
