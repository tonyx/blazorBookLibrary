namespace BookLibrary.Shared.Services

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons

type ITenantApiKeyService =
    abstract member AddPartnerKeyAsync:
        context: UserContext *
        tenantId: TenantId *
        partnerApiKey: PartnerApiKey *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<unit, string>>

    abstract member AddPartnerKeyAsync:
        context: UserContext *
        tenantId: TenantId *
        partnerId: UserId *
        label: string* 
        now: DateTime*
        ?expiresAt: DateTime*
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<unit, string>>

    abstract member RevokeCatalogKeyAsync:
        context: UserContext *
        tenantId: TenantId *
        catalogApiKey: CatalogApiKey *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<unit, string>>

    abstract member ValidateCatalogKeyAsync:
        tenantId: TenantId *
        catalogApiKey: CatalogApiKey *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<unit, string>>

    abstract member RemovePartnerKeyAsync:
        context: UserContext *
        tenantId: TenantId *
        catalogApiKey: CatalogApiKey *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<unit, string>>

    abstract member GetPartnerKeysAsync:
        context: UserContext *
        tenantId: TenantId *
        [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken ->
            Task<Result<List<PartnerApiKey>, string>>
