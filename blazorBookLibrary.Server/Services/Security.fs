namespace BookLibrary.Services
open System.Threading
open Sharpino
open BookLibrary.Domain
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons

module Security =
    let isGlobalAdminOrTenantManager (tenant: Tenant) (context: UserContext) = 
        let isManagerOrOwner = 
            match context with
            | UserContext.Authenticated(userId, _, _ ) -> 
                tenant.OwnerId = userId || 
                (tenant.GetUserRole userId = Some PatronRole.Manager)
            | _ -> false

        match context with
        | UserContext.Authenticated _ when context.IsInRole Role.Admin -> true
        | _ -> isManagerOrOwner

    let checkIsGlobalAdminOrTenantManager (tenant: Tenant) (context: UserContext) = 
        isGlobalAdminOrTenantManager tenant context
        |> Result.ofBool "Access allowed only to admins or managers"

    let checkIsGlobalAdminOrTenantManagerOrPublicTenant (tenant: Tenant) (context: UserContext) = 
        if tenant.Public then Ok ()
        else checkIsGlobalAdminOrTenantManager tenant context

    let checkIsGlobalAdminOrTenantManagerOrSelf (tenant: Tenant) (context: UserContext) (targetUserId: UserId) = 
        match context with
        | UserContext.Authenticated(userId, _, _) when userId = targetUserId -> Ok ()
        | _ -> checkIsGlobalAdminOrTenantManager tenant context
