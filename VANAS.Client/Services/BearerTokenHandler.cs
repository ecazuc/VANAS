using System.Net.Http;
using System.Net.Http.Headers;

namespace VANAS.Client.Services
{
    /// <summary>Attaches the logged-in customer's JWT to every outgoing API request.</summary>
    public class BearerTokenHandler : DelegatingHandler
    {
        private readonly Func<string?> _tokenProvider;

        public BearerTokenHandler(Func<string?> tokenProvider) : base(new HttpClientHandler()) => _tokenProvider = tokenProvider;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = _tokenProvider();
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
