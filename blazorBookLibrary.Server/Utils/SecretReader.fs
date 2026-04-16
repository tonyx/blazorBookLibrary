
namespace BookLibrary.Utils
open System
open Microsoft.Extensions.Configuration
open Azure.Identity;
open Azure.Security.KeyVault.Secrets
open Microsoft.Extensions.Logging
open Azure.Core.Pipeline
open Azure.Core
open System.Threading

type SecretsReader(configuration: IConfiguration) =
    let aspUsersConnectionString =
        if (configuration.GetSection("Environment").Exists()) then
            let env = configuration.GetSection("Environment").Value
            if (env = "Development") then
                configuration.GetSection("ConnectionStrings:UsersDbConnection").Value
            else 
                let keyVaultUrl = configuration.GetValue<string>("KeyVaultUrl", "unexisting")
                if (keyVaultUrl = "unexisting") then
                    failwith "KeyVaultUrl not found in appSettings.json"

                let secretName = configuration.GetValue<string>("usersDbSecretname", "unexisting")
                if (secretName = "unexisting") then
                    failwith "users db SecretName not found in appSettings.json"

                try
                    let options = SecretClientOptions()
                    options.Retry.Delay <- TimeSpan.FromSeconds(2.0)
                    options.Retry.MaxDelay <- TimeSpan.FromSeconds(16.0)
                    options.Retry.MaxRetries <- 5
                    options.Retry.Mode <- RetryMode.Exponential
                    
                    let client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential(), options)
                    let secret =
                        client.GetSecretAsync secretName
                        |> Async.AwaitTask
                        |> Async.RunSynchronously
                    secret.Value.Value
                    
                with
                | _ as ex ->
                    failwith $"Secret password {secretName} not found. Error: {ex.Message}"
        else
            failwith "Environment not found: set Environment entry to appsettings.json to Production or Development"

    let bookLibraryConnectionString =
        if (configuration.GetSection("Environment").Exists()) then
            let env = configuration.GetSection("Environment").Value
            if (env = "Development") then
                configuration.GetSection("ConnectionStrings:BookLibraryDbConnection").Value
            else 
                let keyVaultUrl = configuration.GetValue<string>("KeyVaultUrl", "unexisting")
                if (keyVaultUrl = "unexisting") then
                    failwith "KeyVaultUrl not found in appSettings.json"

                let secretName = configuration.GetValue<string>("booksLibraryDbSecretName", "unexisting")
                if (secretName = "unexisting") then
                    failwith "booksLibraryDbSecretName SecretName not found in appSettings.json"

                try
                    let options = SecretClientOptions()
                    options.Retry.Delay <- TimeSpan.FromSeconds(2.0)
                    options.Retry.MaxDelay <- TimeSpan.FromSeconds(16.0)
                    options.Retry.MaxRetries <- 5
                    options.Retry.Mode <- RetryMode.Exponential
                    
                    let client = new SecretClient(new Uri(keyVaultUrl), new DefaultAzureCredential(), options)
                    let secret =
                        client.GetSecretAsync secretName
                        |> Async.AwaitTask
                        |> Async.RunSynchronously
                    secret.Value.Value
                    
                with
                | _ as ex ->
                    failwith $"Secret password {secretName} not found. Error: {ex.Message}"
        else
            failwith "Environment not found: set Environment entry to appsettings.json to Production or Development"

    member this.GetAspUsersConnectionString () = 
        aspUsersConnectionString

    member this.GetBookLibraryConnectionString () = 
        bookLibraryConnectionString




