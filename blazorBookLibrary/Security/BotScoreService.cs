using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace blazorBookLibrary.Security;

public class BotScoreService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public BotScoreService(IConfiguration configuration)
    {
        _configuration = configuration;
        _httpClient = new HttpClient();
    }

    public async Task<double> GetBotScoreAsync(string token)
    {
        var secret = _configuration["Authentication:Recaptcha:SecretKey"];
        
        var response = await _httpClient.PostAsync(
            $"https://www.google.com/recaptcha/api/siteverify?secret={secret}&response={token}", null);
        
        var result = await response.Content.ReadFromJsonAsync<RecaptchaVerifyResponse>();
        return result?.Success == true ? result.Score : 0.0;
    }
    public async Task ApplyBotDelayAsync(double score)
    {
        if (score < 0.7)
        {
            int delayMilliseconds = (int)((1.0 - score) * 5000); 
            
            await Task.Delay(delayMilliseconds);
        }
    }
}