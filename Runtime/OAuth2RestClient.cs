using CommonUtils.RestSdk;
using UnityEngine.Networking;

namespace GlobalstatsIO {
	public class OAuth2RestClient : RestClient {
		public OAuth2RestClient(string apiUrl) : base(apiUrl) { }

		protected override void SetRequestHeaders(UnityWebRequest www) {
			base.SetRequestHeaders(www);
		}
	}
}