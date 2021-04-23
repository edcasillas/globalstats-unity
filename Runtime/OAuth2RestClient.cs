using CommonUtils.RestSdk;
using UnityEngine.Networking;

namespace GlobalstatsIO {
	public class OAuth2RestClient : RestClient {
		private AccessToken authToken;
		public AccessToken AuthToken => authToken; // TODO Remove this accessor

		public bool HasValidAccessToken {
			get {
				if (authToken == null || !authToken.IsValid()) {
					authToken = null;
				}

				return authToken != null;
			}
		}

		public OAuth2RestClient(string apiUrl) : base(apiUrl) { }

		protected override void SetRequestHeaders(UnityWebRequest www) {
			base.SetRequestHeaders(www);
			www.SetRequestHeader("Authorization", $"Bearer {authToken.access_token}");
		}

		public void SetAccessToken(AccessToken token) => authToken = token;
		public void ClearAccessToken() => authToken = null;
	}
}