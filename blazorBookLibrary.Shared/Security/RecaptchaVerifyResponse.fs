namespace blazorBookLibrary.Shared.Security

open System.Text.Json.Serialization

[<CLIMutable>]
type RecaptchaVerifyResponse =
    { Success: bool
      Score: float
      Action: string
      [<JsonPropertyName("error-codes")>]
      ErrorCodes: System.Collections.Generic.List<string> }