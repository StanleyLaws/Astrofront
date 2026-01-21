using Sandbox;
using System.Threading.Tasks;

public static class BackendClient
{
    // ⚠️ Ton API tourne maintenant sur le port 8080
    private static readonly string BaseUrl = "http://localhost:8080";

    public class AddRequest
    {
        public string steamId { get; set; }
        public string currency { get; set; }
        public long delta { get; set; }
        public string reason { get; set; }
        public string serverId { get; set; }
        public string idempotencyKey { get; set; }
    }

    public class AddResponse
    {
        public bool applied { get; set; }
        public string steamId { get; set; }
        public string currency { get; set; }
        public long delta { get; set; }
        public long newAmount { get; set; }
    }

    public static async Task<AddResponse> AddCurrencyAsync(
        string steamId, string currency, long delta, string reason, string serverId, string idempotencyKey)
    {
        var body = new AddRequest
        {
            steamId = steamId,
            currency = currency,
            delta = delta,
            reason = reason,
            serverId = serverId,
            idempotencyKey = idempotencyKey
        };

        // Crée un HttpContent JSON via l’API S&box
        var content = Http.CreateJsonContent( body );

        // Envoie la requête POST et parse la réponse JSON en AddResponse
        // (Ports localhost autorisés par s&box : 80, 443, 8080, 8443)
        var url = $"{BaseUrl}/currency/add";
        var resp = await Http.RequestJsonAsync<AddResponse>( url, "POST", content );
        return resp;
    }
	
	public class BalanceResponse
	{
		public string steamId { get; set; }
		public string currency { get; set; }
		public long amount { get; set; }
	}

	public static async Task<BalanceResponse> GetBalanceAsync(string steamId, string currency)
	{
		var url = $"{BaseUrl}/currency/balance?steamId={System.Uri.EscapeDataString(steamId)}&currency={System.Uri.EscapeDataString(currency)}";
		// Utilise l’API HTTP de S&box pour obtenir du JSON
		var resp = await Http.RequestJsonAsync<BalanceResponse>( url, "GET", null );
		return resp;
	}

	
	public class RankGetResponse
	{
		public string steamId { get; set; }
		public string rank { get; set; } // "ADMIN" | "MODERATOR" | "VIP" | "PLAYER"
	}

	public static async Task<RankGetResponse> GetRankAsync(string steamId)
	{
		var url = $"{BaseUrl}/rank/get?steamId={System.Uri.EscapeDataString(steamId)}";
		return await Http.RequestJsonAsync<RankGetResponse>( url, "GET", null );
	}

	public class RankSetRequestBody
	{
		public string steamId { get; set; }
		public string rank { get; set; }
	}

	public static async Task<RankGetResponse> SetRankAsync(string steamId, string rankUpper)
	{
		var body = new RankSetRequestBody { steamId = steamId, rank = rankUpper };
		var content = Http.CreateJsonContent( body );
		var url = $"{BaseUrl}/rank/set";
		// L’API renvoie { steamId, rank }
		return await Http.RequestJsonAsync<RankGetResponse>( url, "POST", content );
	}


	
}
