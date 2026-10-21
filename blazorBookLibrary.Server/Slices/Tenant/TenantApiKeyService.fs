namespace BookLibrary.Services

open System
open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Sharpino
open Sharpino.Core
open Sharpino.CommandHandler
open Sharpino.Storage
open Sharpino.EventBroker
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Services
open BookLibrary.Utils
open Microsoft.Extensions.Configuration

type TenantApiKeyService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        tenantViewerAsync: AggregateViewerAsync2<Tenant>,
        configuration: IConfiguration
    ) =

    new(secretsReader: SecretsReader, configuration: IConfiguration) =
        let connectionString = secretsReader.GetBookLibraryConnectionString()
        let messageSenders = MessageSenders.NoSender
        let eventStore = PgStorage.PgEventStore connectionString
        let tenantViewerAsync =
            getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore
        TenantApiKeyService(eventStore, messageSenders, tenantViewerAsync, configuration)

    member this.AddPartnerKey(context: UserContext, tenantId: TenantId, partnerApiKey: PartnerApiKey, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            do! Security.checkIsGlobalAdminOrTenantManager tenant context
            let cmd = TenantCommand.AddPartnerKey partnerApiKey
            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    context.Metadata
                    cmd
                    ct
        }

    member this.AddPartnerKey (context: UserContext, tenantId: TenantId, partnerId: UserId, label: string, now: DateTime, ?expiresAt: DateTime, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            do! Security.checkIsGlobalAdminOrTenantManager tenant context

            let newKey = CatalogApiKey.New()
            let partnerKey =
                {
                    Key = newKey
                    PartnerId = partnerId
                    Label = label
                    CreatedAt = now
                    RevokedAt = None
                    ExpiresAt = expiresAt
                }

            let cmd = TenantCommand.AddPartnerKey partnerKey
            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    context.Metadata
                    cmd
                    ct
        }

    member this.RevokeCatalogKey(context: UserContext, tenantId: TenantId, catalogApiKey: CatalogApiKey, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            do! Security.checkIsGlobalAdminOrTenantManager tenant context
            let now = DateTime.UtcNow
            let cmd = TenantCommand.RevokeCatalogKey(catalogApiKey, now)
            return!
                runAggregateCommandMdAsync<Tenant, TenantEvent, string>
                    tenantId.Value
                    eventStore
                    messageSenders
                    context.Metadata
                    cmd
                    ct
        }

    member this.ValidateCatalogKey(tenantId: TenantId, catalogApiKey: CatalogApiKey, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            let configuredKeyOpt =
                let cfg = configuration.["Catalog:ApiKey"]
                if String.IsNullOrWhiteSpace cfg then None
                else Some (CatalogApiKey cfg)
            return! tenant.ValidateCatalogKey(catalogApiKey, ?configuredKey = configuredKeyOpt)
        }

    member this.GetPartnerKeys(context: UserContext, tenantId: TenantId, ?ct: CancellationToken) =
        taskResult {
            let! (_, tenant) = tenantViewerAsync ct tenantId.Value
            do! Security.checkIsGlobalAdminOrTenantManager tenant context
            return tenant.PartnerApiKeys
        }

    interface ITenantApiKeyService with
        member this.AddPartnerKeyAsync(context, tenantId, partnerApiKey, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            this.AddPartnerKey(context, tenantId, partnerApiKey, ct)

        member this.AddPartnerKeyAsync(context, tenantId, partnerId, label, now, ?expiresAt, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            this.AddPartnerKey(context, tenantId, partnerId, label, now, ?expiresAt = expiresAt, ct = ct)

        member this.RevokeCatalogKeyAsync(context, tenantId, catalogApiKey, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            this.RevokeCatalogKey(context, tenantId, catalogApiKey, ct)

        member this.ValidateCatalogKeyAsync(tenantId, catalogApiKey, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            this.ValidateCatalogKey(tenantId, catalogApiKey, ct)

        member this.GetPartnerKeysAsync(context, tenantId, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            this.GetPartnerKeys(context, tenantId, ct)
