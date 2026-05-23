
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons

type ITenantService = 
    abstract member EnsureDefaultTenantExistsAsync: userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member CreateTenantAsync: UserContext * Tenant * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member GetTenantAsync: UserContext * TenantId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<Tenant, string>>
    abstract member AddPatronAsync: UserContext * TenantId * UserId * PatronRole * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member DemotePatronAsync: UserContext * TenantId * UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member PromotePatronAsync: UserContext * TenantId * UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member RemovePatronAsync: UserContext * TenantId * UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member InvitePatronAsync: UserContext * TenantId * UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member ConvertInvitedPatronToPatronAsync: UserContext * TenantId * PatronInvitationCode * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member RevokePatronInvitation: UserContext * TenantId * UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member SuspendPatron: UserContext * TenantId * UserId * reason:string * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member ReAdmittPatron: UserContext * TenantId * UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member GetUserRoleAsync: UserContext * TenantId * UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<PatronRole, string>> 
    abstract member GetAllPublicTenantsAsync: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<List<Tenant>, string>> 
    abstract member GetAllowedTenantsAsync: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<List<Tenant>, string>>
    abstract member GetMyTenantsAsync: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<List<Tenant>, string>>
    abstract member GetMyOwnedTenantsAsync: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<List<Tenant>, string>>
    abstract member SetPublicAsync: UserContext * TenantId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member SetPrivateAsync: UserContext * TenantId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member DeleteTenantAsync: UserContext * TenantId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>