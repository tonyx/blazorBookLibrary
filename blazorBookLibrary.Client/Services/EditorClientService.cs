using System.Net.Http.Json;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;
using System.Runtime.InteropServices;
using Microsoft.FSharp.Collections;

namespace blazorBookLibrary.Client.Services;

public class EditorClientService : IEditorService
{
    private readonly HttpClient _httpClient;

    public EditorClientService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FSharpResult<Editor, string>> AddEditorAsync(Commons.UserContext context, Editor editor, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync("api/Editors", editor, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<Editor>(response);
    }

    public async Task<FSharpResult<Editor, string>> GetEditorAsync(Commons.UserContext context, Commons.EditorId id, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync($"api/Editors/{id.Value}", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<Editor>(response);
    }

    public async Task<FSharpResult<Editor, string>> RenameAsync(Commons.UserContext context, Commons.EditorId editorId, Commons.Name newName, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/Editors/{editorId.Value}/rename", newName.Value, ServiceClientHelper.JsonOptions, ServiceClientHelper.GetValue(ct, CancellationToken.None));
        return await ServiceClientHelper.HandleResponse<Editor>(response);
    }

    public async Task<FSharpResult<FSharpList<Editor>, string>> GetAllAsync(Commons.UserContext context, FSharpOption<CancellationToken> ct)
    {
        var response = await _httpClient.GetAsync("api/Editors", ServiceClientHelper.GetValue(ct, CancellationToken.None));
        var result = await ServiceClientHelper.HandleResponse<List<Editor>>(response);
        return result.IsOk ? FSharpResult<FSharpList<Editor>, string>.NewOk(ListModule.OfSeq(result.ResultValue)) : FSharpResult<FSharpList<Editor>, string>.NewError(result.ErrorValue);
    }
}
