using System;

namespace GlobalstatsIO {
	[Serializable]
	public class AccessToken {
		public string access_token = null;
		public string token_type = null;
		public string expires_in = null;
		public int created_at = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

		/// <summary>
		/// Check if still valid, allow a 2 minute grace period
		/// </summary>
		public bool IsValid() =>
			(this.created_at + int.Parse(this.expires_in) - 120) > (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	}
}