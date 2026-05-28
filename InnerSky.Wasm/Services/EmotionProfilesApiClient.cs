using System.Globalization;
using System.Net.Http.Json;
using InnerSky.Wasm.Models;

namespace InnerSky.Wasm.Services;

public sealed class EmotionProfilesApiClient(HttpClient httpClient)
{
    public async Task<EmotionProfileApiResponse?> CreateAsync(EmotionProfile profile, int? momentId, EmotionMomentApiRequest? newMoment, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/emotionprofiles", new EmotionProfileApiRequest(
            profile.Name,
            profile.Components.Select(c => new EmotionProfileApiComponentRequest(c.Emotion, (int)c.Level)).ToList(),
            momentId,
            newMoment), cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EmotionProfileApiResponse>(cancellationToken);
    }

    public async Task<IReadOnlyList<EmotionProfileApiResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await httpClient.GetFromJsonAsync<List<EmotionProfileApiResponse>>("api/emotionprofiles", cancellationToken);
        return profiles ?? [];
    }

    public async Task<EmotionMomentListApiResponse> ListMomentsAsync(MomentsQuery? query = null, CancellationToken cancellationToken = default)
    {
        var url = "api/emotionprofiles/moments";
        var queryString = BuildMomentsQueryString(query);
        if (!string.IsNullOrEmpty(queryString))
        {
            url += "?" + queryString;
        }

        var moments = await httpClient.GetFromJsonAsync<EmotionMomentListApiResponse>(url, cancellationToken);
        return moments ?? new EmotionMomentListApiResponse([], 0, query?.Page ?? 1, query?.PageSize ?? 12, 0);
    }

    public async Task<EmotionMomentApiResponse?> GetMomentAsync(int id, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<EmotionMomentApiResponse>($"api/emotionprofiles/moments/{id}", cancellationToken);

    public async Task<EmotionMomentApiResponse?> UpdateMomentAsync(int id, EmotionMomentUpdateApiRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/emotionprofiles/moments/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EmotionMomentApiResponse>(cancellationToken);
    }

    public async Task UpdateBlendOrderAsync(int momentId, IReadOnlyList<int> blendIds, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"api/emotionprofiles/moments/{momentId}/blend-order", new EmotionMomentBlendOrderApiRequest(blendIds), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveBlendAsync(int momentId, int blendId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync($"api/emotionprofiles/moments/{momentId}/blends/{blendId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string BuildMomentsQueryString(MomentsQuery? query)
    {
        if (query is null)
        {
            return string.Empty;
        }

        var values = new List<string>
        {
            $"page={query.Page}",
            $"pageSize={query.PageSize}"
        };

        Add(values, "search", query.Search);
        Add(values, "emotion", query.Emotion);
        Add(values, "fromUtc", query.FromUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Add(values, "toUtc", query.ToUtc?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

        return string.Join('&', values);
    }

    private static void Add(List<string> values, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }
    }
}

public sealed record MomentsQuery(string? Search, string? Emotion, DateTime? FromUtc, DateTime? ToUtc, int Page, int PageSize);
public sealed record EmotionProfileApiRequest(string Name, IReadOnlyList<EmotionProfileApiComponentRequest> Components, int? MomentId, EmotionMomentApiRequest? NewMoment);
public sealed record EmotionMomentApiRequest(string? Title, string? Comment, DateTime MomentUtc);
public sealed record EmotionMomentUpdateApiRequest(string Title, string? Comment, DateTime MomentUtc);
public sealed record EmotionMomentBlendOrderApiRequest(IReadOnlyList<int> BlendIds);
public sealed record EmotionProfileApiComponentRequest(string Emotion, int Level);
public sealed record EmotionMomentListApiResponse(IReadOnlyList<EmotionMomentApiResponse> Items, int TotalCount, int Page, int PageSize, int TotalPages);
public sealed record EmotionProfileApiResponse(int Id, string Name, DateTime CreatedUtc, int MomentId, int SortOrder, IReadOnlyList<EmotionProfileApiComponentResponse> Components);
public sealed record EmotionProfileApiComponentResponse(string Emotion, int Level);
public sealed record EmotionMomentApiResponse(int Id, string Title, string? Comment, DateTime MomentUtc, DateTime CreatedUtc, IReadOnlyList<EmotionProfileApiResponse> Blends);
