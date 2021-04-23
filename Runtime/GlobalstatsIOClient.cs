using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GlobalstatsIO {
	public class GlobalstatsIOClient {
		/*#region Singleton
		private static GlobalstatsIOClient instance;
		public static GlobalstatsIOClient Instance { get; private set; }
		private GlobalstatsIOClient() { }
		#endregion*/

		private readonly string _apiId;
		private readonly string _apiSecret;
		private AccessToken _apiAccessToken;
		private List<StatisticValues> _statisticValues = new List<StatisticValues>();

		public string StatisticId { get; set; } = "";
		public string UserName { get; set; } = "";
		public LinkData LinkData { get; set; } = null;

		#region Serializable classes
		[Serializable]
		private class AccessToken {
			public string access_token = null;
			public string token_type = null;
			public string expires_in = null;
			public int created_at = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

			//Check if still valid, allow a 2 minute grace period
			public bool IsValid() =>
				(this.created_at + int.Parse(this.expires_in) - 120) > (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		}

		[Serializable]
		private class StatisticResponse {
			public string name = null;
			public string _id = null;

			[SerializeField]
			public List<StatisticValues> values = null;
		}
		#endregion

		public GlobalstatsIOClient(string apiKey, string apiSecret) {
			this._apiId = apiKey;
			this._apiSecret = apiSecret;
		}

		private IEnumerator getAccessToken() {
			string url = "https://api.globalstats.io/oauth/access_token";

			WWWForm form = new WWWForm();
			form.AddField("grant_type", "client_credentials");
			form.AddField("scope", "endpoint_client");
			form.AddField("client_id", this._apiId);
			form.AddField("client_secret", this._apiSecret);

			using (UnityWebRequest www = UnityWebRequest.Post(url, form)) {
				www.downloadHandler = new DownloadHandlerBuffer();
				yield return www.SendWebRequest();

				string responseBody = www.downloadHandler.text;

				if (www.isNetworkError || www.isHttpError) {
					Debug.LogWarning("Error retrieving access token: " + www.error);
					Debug.Log("GlobalstatsIO API Response: " + responseBody);
					yield break;
				} else {
					this._apiAccessToken = JsonUtility.FromJson<AccessToken>(responseBody);
				}
			}
		}

		public IEnumerator Share(Dictionary<string, string> values, string id = "", string name = "",
			Action<bool> callback = null) {
			var update = false;

			if (_apiAccessToken == null || !_apiAccessToken.IsValid()) {
				yield return getAccessToken();
			}

			// If no id is supplied but we have one stored, reuse it.
			if (string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(StatisticId)) {
				id = StatisticId;
			}

			var url = "https://api.globalstats.io/v1/statistics";
			if (!string.IsNullOrEmpty(id)) {
				url += $"/{id}";
				update = true;
			} else {
				if (string.IsNullOrWhiteSpace(name)) {
					name = "anonymous";
				}
			}

			string jsonPayload;

			if (update == false) {
				jsonPayload = "{\"name\":\"" + name + "\", \"values\":{";
			} else {
				jsonPayload = "{\"values\":{";
			}

			var semicolon = false;
			foreach (var value in values) {
				if (semicolon) {
					jsonPayload += ",";
				}

				jsonPayload += "\"" + value.Key + "\":\"" + value.Value + "\"";
				semicolon = true;
			}

			jsonPayload += "}}";

			var pData = Encoding.UTF8.GetBytes(jsonPayload);
			StatisticResponse statistic = null;

			using (var www = new UnityWebRequest(url)) {
				www.method = update == false ? "POST" : "PUT";

				www.uploadHandler = new UploadHandlerRaw(pData);
				www.downloadHandler = new DownloadHandlerBuffer();
				www.SetRequestHeader("Authorization", "Bearer " + this._apiAccessToken.access_token);
				www.SetRequestHeader("Content-Type", "application/json");
				yield return www.SendWebRequest();

				var responseBody = www.downloadHandler.text;

				if (www.isNetworkError || www.isHttpError) {
					Debug.LogWarning("Error submitting statistic: " + www.error);
					Debug.Log("GlobalstatsIO API Response: " + responseBody);
					callback?.Invoke(false);
				} else {
					statistic = JsonUtility.FromJson<StatisticResponse>(responseBody);
				}
			}

			// ID is available only on create, not on update, so do not overwrite it
			if (statistic?._id != null && statistic._id != "") {
				StatisticId = statistic._id;
			}

			UserName = statistic?.name;

			if (statistic != null) {
				//Store the returned data statically
				foreach (var value in statistic.values) {
					bool updatedExisting = false;
					for (int i = 0; i < this._statisticValues.Count; i++) {
						if (this._statisticValues[i].key == value.key) {
							this._statisticValues[i] = value;
							updatedExisting = true;
							break;
						}
					}

					if (!updatedExisting) {
						this._statisticValues.Add(value);
					}
				}
			}

			callback?.Invoke(true);
		}

		public StatisticValues GetStatistic(string key) {
			for (int i = 0; i < this._statisticValues.Count; i++) {
				if (this._statisticValues[i].key == key) {
					return this._statisticValues[i];
				}
			}

			return null;
		}

		public IEnumerator LinkStatistic(string id = "", Action<bool> callback = null) {
			if (this._apiAccessToken == null || !this._apiAccessToken.IsValid()) {
				yield return this.getAccessToken();
			}

			// If no id is supplied but we have one stored, reuse it.
			if (id == "" && this.StatisticId != "") {
				id = this.StatisticId;
			}

			string url = "https://api.globalstats.io/v1/statisticlinks/" + id + "/request";

			string jsonPayload = "{}";
			byte[] pData = Encoding.UTF8.GetBytes(jsonPayload);

			using (UnityWebRequest www = new UnityWebRequest(url, "POST") {
				uploadHandler = new UploadHandlerRaw(pData),
				downloadHandler = new DownloadHandlerBuffer()
			}) {
				www.SetRequestHeader("Authorization", "Bearer " + this._apiAccessToken.access_token);
				www.SetRequestHeader("Content-Type", "application/json");
				yield return www.SendWebRequest();

				string responseBody = www.downloadHandler.text;

				if (www.isNetworkError || www.isHttpError) {
					Debug.LogWarning("Error linking statistic: " + www.error);
					Debug.Log("GlobalstatsIO API Response: " + responseBody);
					callback?.Invoke(false);
				}

				this.LinkData = JsonUtility.FromJson<LinkData>(responseBody);
			}

			;

			callback?.Invoke(true);
		}

		public IEnumerator GetLeaderboard(string gtd, int numberOfPlayers, Action<Leaderboard> callback) {
			numberOfPlayers = Mathf.Clamp(numberOfPlayers, 0, 100); // make sure numberOfPlayers is between 0 and 100

			if (this._apiAccessToken == null || !this._apiAccessToken.IsValid()) {
				yield return this.getAccessToken();
			}

			string url = "https://api.globalstats.io/v1/gtdleaderboard/" + gtd;

			string json_payload = "{\"limit\":" + numberOfPlayers + "\n}";
			Leaderboard leaderboard;
			byte[] pData = Encoding.UTF8.GetBytes(json_payload);

			using (UnityWebRequest www = new UnityWebRequest(url, "POST") {
				uploadHandler = new UploadHandlerRaw(pData),
				downloadHandler = new DownloadHandlerBuffer()
			}) {
				www.SetRequestHeader("Authorization", "Bearer " + this._apiAccessToken.access_token);
				www.SetRequestHeader("Content-Type", "application/json");
				yield return www.SendWebRequest();

				string responseBody = www.downloadHandler.text;

				if (www.isNetworkError || www.isHttpError) {
					Debug.LogWarning("Error getting leaderboard: " + www.error);
					Debug.Log("GlobalstatsIO API Response: " + responseBody);
					callback?.Invoke(null);
				}

				leaderboard = JsonUtility.FromJson<Leaderboard>(responseBody);
			}

			;

			callback?.Invoke(leaderboard);
		}
	}
}