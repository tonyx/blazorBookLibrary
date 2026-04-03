using System.Text.Json.Serialization;

namespace blazorBookLibrary.Security;

public class RecaptchaVerifyResponse
{
    public bool Success { get; set; }
    public double Score { get; set; }
    public string Action { get; set; }
    [JsonPropertyName("error-codes")]
    public List<string>? ErrorCodes { get; set; }
}