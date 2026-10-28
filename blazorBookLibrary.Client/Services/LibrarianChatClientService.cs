using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Shared;
using System.Threading;
using System.Threading.Tasks;

namespace blazorBookLibrary.Client.Services;

public class LibrarianChatClientService : ILibrarianChatService
{
    private readonly HttpClient _httpClient;

    public LibrarianChatClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<LibrarianChatResponse, string>> ChatAsync(
        Commons.UserContext context,
        LibrarianChatRequest request,
        FSharpOption<CancellationToken> ct)
    {
        var httpRequest = ServiceClientHelper.CreateRequest(
            HttpMethod.Post,
            "api/LibrarianChat/chat",
            context,
            request);

        var response = await _httpClient.SendAsync(
            httpRequest,
            ServiceClientHelper.GetValue(ct, CancellationToken.None));

        return await ServiceClientHelper.HandleResponse<LibrarianChatResponse>(response);
    }
}
