
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
            let userContext = UserContext.Authenticated(userId, [])
            
            let tenant = Tenant.New(userId, TenantName.New "My Personal Library" |> Result.get, "123 Main St")
            let! result = tenantService.CreateTenantAsync(userContext, tenant)
            
            Expect.isOk result "User should be able to create their own tenant"
        }
        testCaseTask "a user cannot create a tenant for someone else" <| fun _ -> task {
            setUp()
            let tenantService = getTenantService()
            
            let! userId1 = registerUserTask "user1@test.com" "Password123!"
            let! userId2 = registerUserTask "user2@test.com" "Password123!"
            
            let user1Context = UserContext.Authenticated(userId1, [])
            
            // User 1 tries to create a tenant for User 2
            let tenant = Tenant.New(userId2, TenantName.New "Stolen Library" |> Result.get, "123 Main St")
            let! result = tenantService.CreateTenantAsync(user1Context, tenant)
            
            Expect.isError result "User should not be able to create a tenant for another user"
        }
        testCaseTask "a user can retrieve a public tenant created by another user" <| fun _ -> task {
            setUp()
            let tenantService = getTenantService()
            
            let! ownerId = registerUserTask "owner@test.com" "Password123!"
            let ownerContext = UserContext.Authenticated(ownerId, [])
            
            // Create a public tenant (true is default)
            let tenant = Tenant.New(ownerId, TenantName.New "Public Library" |> Result.get, "123 Main St", true)
            let tenantId = tenant.TenantId
            let! createResult = tenantService.CreateTenantAsync(ownerContext, tenant)
            Expect.isOk createResult "Tenant creation should succeed"
            
            let! otherUserId = registerUserTask "other@test.com" "Password123!"
            let otherUserContext = UserContext.Authenticated(otherUserId, [])
            
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
            let ownerContext = UserContext.Authenticated(ownerId, [])
            
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
            let ownerContext = UserContext.Authenticated(ownerId, [])
            
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
            let ownerContext = UserContext.Authenticated(ownerId, [])
            
            // Create a private tenant
            let tenant = Tenant.New(ownerId, TenantName.New "Private Library" |> Result.get, "123 Main St", false)
            let tenantId = tenant.TenantId
            let! createResult = tenantService.CreateTenantAsync(ownerContext, tenant)
            Expect.isOk createResult "Tenant creation should succeed"
            
            let! otherUserId = registerUserTask "other@test.com" "Password123!"
            let otherUserContext = UserContext.Authenticated(otherUserId, [])
            
            // Other user tries to retrieve the private tenant
            let! getResult = tenantService.GetTenantAsync(otherUserContext, tenantId)
            
            Expect.isError getResult "Non-admin user should not be able to retrieve private tenant"
        }
        testCaseTask "owner can add a patron to their tenant" <| fun _ -> task {
            setUp()
            let tenantService = getTenantService()
            let! ownerId = registerUserTask "owner@test.com" "Password123!"
            let ownerContext = UserContext.Authenticated(ownerId, [])
            let tenant = Tenant.New(ownerId, TenantName.New "My Library" |> Result.get, "123 Main St")
            let tenantId = tenant.TenantId
            let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)
            
            let! patronId = registerUserTask "patron@test.com" "Password123!"
            let! result = tenantService.AddPatronAsync(ownerContext, tenantId, patronId, PatronRole.User)
            
            Expect.isOk result "Owner should be able to add a patron"
            
            let! getResult = tenantService.GetTenantAsync(ownerContext, tenantId)
            match getResult with
            | Ok t -> 
                Expect.exists t.Patrons (fun (u, r) -> u = patronId && r = PatronRole.User) "Patron should be in the list"
            | Error msg -> failwith msg
        }
        testCaseTask "owner can promote and demote a patron" <| fun _ -> task {
            setUp()
            let tenantService = getTenantService()
            let! ownerId = registerUserTask "owner@test.com" "Password123!"
            let ownerContext = UserContext.Authenticated(ownerId, [])
            let tenant = Tenant.New(ownerId, TenantName.New "My Library" |> Result.get, "123 Main St")
            let tenantId = tenant.TenantId
            let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)
            
            let! patronId = registerUserTask "patron@test.com" "Password123!"
            let! _ = tenantService.AddPatronAsync(ownerContext, tenantId, patronId, PatronRole.User)
            
            let! promoteResult = tenantService.PromotePatronAsync(ownerContext, tenantId, patronId)
            Expect.isOk promoteResult "Owner should be able to promote a patron"
            
            let! getResult1 = tenantService.GetTenantAsync(ownerContext, tenantId)
            match getResult1 with
            | Ok t -> Expect.exists t.Patrons (fun (u, r) -> u = patronId && r = PatronRole.Manager) "Patron should be a manager"
            | Error msg -> failwith msg
            
            let! demoteResult = tenantService.DemotePatronAsync(ownerContext, tenantId, patronId)
            Expect.isOk demoteResult "Owner should be able to demote a patron"
            
            let! getResult2 = tenantService.GetTenantAsync(ownerContext, tenantId)
            match getResult2 with
            | Ok t -> Expect.exists t.Patrons (fun (u, r) -> u = patronId && r = PatronRole.User) "Patron should be a user again"
            | Error msg -> failwith msg
        }
        testCaseTask "owner can remove a patron" <| fun _ -> task {
            setUp()
            let tenantService = getTenantService()
            let! ownerId = registerUserTask "owner@test.com" "Password123!"
            let ownerContext = UserContext.Authenticated(ownerId, [])
            let tenant = Tenant.New(ownerId, TenantName.New "My Library" |> Result.get, "123 Main St")
            let tenantId = tenant.TenantId
            let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)
            
            let! patronId = registerUserTask "patron@test.com" "Password123!"
            let! _ = tenantService.AddPatronAsync(ownerContext, tenantId, patronId, PatronRole.User)
            
            let! removeResult = tenantService.RemovePatronAsync(ownerContext, tenantId, patronId)
            Expect.isOk removeResult "Owner should be able to remove a patron"
            
            let! getResult = tenantService.GetTenantAsync(ownerContext, tenantId)
            match getResult with
            | Ok t -> Expect.isFalse (t.Patrons |> List.exists (fun (u, _) -> u = patronId)) "Patron should be removed"
            | Error msg -> failwith msg
        }
        testCaseTask "non-owner cannot add a patron" <| fun _ -> task {
            setUp()
            let tenantService = getTenantService()
            let! ownerId = registerUserTask "owner@test.com" "Password123!"
            let ownerContext = UserContext.Authenticated(ownerId, [])
            let tenant = Tenant.New(ownerId, TenantName.New "My Library" |> Result.get, "123 Main St")
            let tenantId = tenant.TenantId
            let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)
            
            let! otherUserId = registerUserTask "other@test.com" "Password123!"
            let otherContext = UserContext.Authenticated(otherUserId, [])
            
            let! patronId = registerUserTask "patron@test.com" "Password123!"
            let! result = tenantService.AddPatronAsync(otherContext, tenantId, patronId, PatronRole.User)
            
            Expect.isError result "Non-owner should not be able to add a patron"
        }
        testCaseTask "a patron can retrieve their own role" <| fun _ -> task {
            setUp()
            let tenantService = getTenantService()
            let! ownerId = registerUserTask "owner@test.com" "Password123!"
            let ownerContext = UserContext.Authenticated(ownerId, [])
            let tenant = Tenant.New(ownerId, TenantName.New "My Library" |> Result.get, "123 Main St", false)
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
        testCaseTask "a patron can retrieve a private tenant they belong to" <| fun _ -> task {
            setUp()
            let tenantService = getTenantService()
            let! ownerId = registerUserTask "owner@test.com" "Password123!"
            let ownerContext = UserContext.Authenticated(ownerId, [])
            let tenant = Tenant.New(ownerId, TenantName.New "Private Library" |> Result.get, "123 Main St", false)
            let tenantId = tenant.TenantId
            let! _ = tenantService.CreateTenantAsync(ownerContext, tenant)
            
            let! patronId = registerUserTask "patron@test.com" "Password123!"
            let! _ = tenantService.AddPatronAsync(ownerContext, tenantId, patronId, PatronRole.User)
            
            let patronContext = UserContext.Authenticated(patronId, []).WithNewTenant(tenantId)
            let! getResult = tenantService.GetTenantAsync(patronContext, tenantId)
            
            Expect.isOk getResult "Patron should be able to retrieve a private tenant they belong to"
        }
    ]
