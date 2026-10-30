using System.Net;
using System.Net.Http;
using Refit;

namespace VANAS.Client.Services
{
    public static class ApiErrors
    {
        /// <summary>Turns a failed API call into a message a customer can act on.</summary>
        public static string Describe(IApiResponse response)
        {
            var body = response.Error?.Content?.Trim().Trim('"');
            if (!string.IsNullOrEmpty(body) && !body.StartsWith('{')) return body;

            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Identifiants invalides.",
                HttpStatusCode.PaymentRequired => "Plus de temps sur ce compte. Voir le comptoir.",
                HttpStatusCode.Forbidden => "Accès refusé.",
                _ => $"Erreur serveur ({(int)response.StatusCode})."
            };
        }

        public static string Describe(Exception ex) =>
            ex is HttpRequestException or TaskCanceledException
                ? "Serveur injoignable. Vérifiez le réseau."
                : "Erreur inattendue : " + ex.Message;
    }
}
