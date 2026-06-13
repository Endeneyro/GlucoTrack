using Blazored.LocalStorage;

namespace GlucoTrack.Client.Services;

public class AuthHttpHandler : DelegatingHandler
{
    private readonly ILocalStorageService _storage;

    public AuthHttpHandler(ILocalStorageService storage)
    {
        _storage = storage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _storage.GetItemAsync<string>("access_token");
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new("Bearer", token);

        return await base.SendAsync(request, cancellationToken);
    }
}
