using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.FSharp.Core;
using BookLibrary.Shared.Services;
using BookLibrary.Domain;
using BookLibrary.Shared;

namespace blazorBookLibrary.Client.Services
{
    public class InAppMessagesRetrieverClientService : IInAppMessagesRetriever
    {
        private readonly HttpClient _httpClient;

        public InAppMessagesRetrieverClientService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<FSharpResult<string, string>> GetLoanNotificationInAppAsync(
            Commons.Title bookTitle,
            Commons.TenantName tenantName,
            string distributionPoint,
            DateTime loanDate,
            DateTime dueDate,
            Commons.ShortLang shortLang,
            FSharpOption<CancellationToken> ct)
        {
            var request = new
            {
                BookTitle = bookTitle.Value,
                TenantName = tenantName.Value,
                DistributionPoint = distributionPoint,
                LoanDate = loanDate,
                DueDate = dueDate,
                ShortLang = shortLang.Value
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/InAppMessagesRetriever/loan",
                request,
                ServiceClientHelper.JsonOptions,
                ServiceClientHelper.GetValue(ct, CancellationToken.None)
            );

            return await ServiceClientHelper.HandleResponse<string>(response);
        }

        public async Task<FSharpResult<string, string>> GetReleaseLoanNotificationInAppAsync(
            string userName,
            Commons.Title bookTitle,
            DateTime loanedAt,
            DateTime returnedAt,
            Commons.TenantName tenantName,
            Commons.NonEmptyName dpName,
            Commons.ShortLang shortLang,
            FSharpOption<CancellationToken> ct)
        {
            var request = new
            {
                UserName = userName,
                BookTitle = bookTitle.Value,
                LoanedAt = loanedAt,
                ReturnedAt = returnedAt,
                TenantName = tenantName.Value,
                DpName = dpName.Value,
                ShortLang = shortLang.Value
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/InAppMessagesRetriever/release-loan",
                request,
                ServiceClientHelper.JsonOptions,
                ServiceClientHelper.GetValue(ct, CancellationToken.None)
            );

            return await ServiceClientHelper.HandleResponse<string>(response);
        }

        public async Task<FSharpResult<string, string>> GetReservationNotificationInAppAsync(
            Commons.Title bookTitle,
            Commons.ReservationCode code,
            Commons.TenantName tenantName,
            string distributionPoint,
            Commons.ShortLang shortLang,
            FSharpOption<CancellationToken> ct)
        {
            var request = new
            {
                BookTitle = bookTitle.Value,
                Code = code.Value,
                TenantName = tenantName.Value,
                DistributionPoint = distributionPoint,
                ShortLang = shortLang.Value
            };

            var response = await _httpClient.PostAsJsonAsync(
                "api/InAppMessagesRetriever/reservation",
                request,
                ServiceClientHelper.JsonOptions,
                ServiceClientHelper.GetValue(ct, CancellationToken.None)
            );

            return await ServiceClientHelper.HandleResponse<string>(response);
        }

        public async Task<FSharpResult<string, string>> GetPatronInvitationInAppAsync(
            Commons.ShortLang shortLang,
            FSharpOption<CancellationToken> ct)
        {
            var response = await _httpClient.GetAsync(
                $"api/InAppMessagesRetriever/patron-invitation?shortLang={Uri.EscapeDataString(shortLang.Value)}",
                ServiceClientHelper.GetValue(ct, CancellationToken.None)
            );

            return await ServiceClientHelper.HandleResponse<string>(response);
        }
    }
}
