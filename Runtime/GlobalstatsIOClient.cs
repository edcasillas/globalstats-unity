using CommonUtils;
using CommonUtils.Extensions;
using CommonUtils.RestSdk;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GlobalstatsIO {
	public class GlobalstatsIOClient : IVerbosable {
		private const string STATISTIC_ID_PREF_KEY = "globalstats.io.statistic-id";
		private const string BASE_URL = "https://api.globalstats.io";

		#region Singleton
		private static GlobalstatsIOClient instance;

		public static GlobalstatsIOClient Instance => instance ??= new GlobalstatsIOClient();

		private GlobalstatsIOClient() {
			if(!ApiConfig.Init()) return;
			_apiId = ApiConfig.Instance.GlobalstatsId;
			_apiSecret = ApiConfig.Instance.GlobalstatsSecret;
			restClient = new OAuth2RestClient(BASE_URL);
		}
		#endregion

		private readonly OAuth2RestClient restClient;
		private readonly string _apiId;
		private readonly string _apiSecret;
		private List<StatisticValues> _statisticValues = new List<StatisticValues>();

		public string StatisticId {
			get => PlayerPrefs.GetString(STATISTIC_ID_PREF_KEY);
			set => PlayerPrefs.SetString(STATISTIC_ID_PREF_KEY, value);
		}

		public string UserName { get; set; } = "";
		public LinkData LinkData { get; set; } = null;

		public bool IsVerbose => ApiConfig.Instance.IsVerbose;

		#region Serializable classes
		[Serializable]
		private class StatisticResponse {
			public string name = null;
			public string _id = null;

			[SerializeField]
			public List<StatisticValues> values = null;

			public UserStatistics ToUserStatistics() {
				var result = new UserStatistics {name = name, statistics = values};
				return result;
			}
		}
		#endregion

		private void ensureAccessToken(Action onSuccess = null, Action onError = null) {
			if (restClient.HasValidAccessToken) {
				this.DebugLogNoContext("Globalstats.io: Valid access token found.");
				onSuccess?.Invoke();
				return;
			}
			this.DebugLogNoContext("Globalstats.io: Fetching access token from globalstats.io");
			restClient.Post<AccessToken>("oauth/access_token",
				new Dictionary<string, object>() {
					{"grant_type", "client_credentials"},
					{"scope", "endpoint_client"},
					{"client_id", ApiConfig.Instance.GlobalstatsId},
					{"client_secret", ApiConfig.Instance.GlobalstatsSecret}
				},
				response => {
					if (response.IsSuccess) {
						this.DebugLogNoContext("Globalstats.io: Access token successfully retrieved.");
						restClient.SetAccessToken(response.Data);
						onSuccess?.Invoke();
					} else {
						Debug.LogError("Globalstats.io: Access token could not be retrieved.");
						onError?.Invoke();
					}
				});
		}

		[Obsolete]
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
					restClient.SetAccessToken(JsonUtility.FromJson<AccessToken>(responseBody));
				}
			}
		}

		#region Share
		/// <summary>
		/// Asynchronously submits the specified <paramref name="values"/>.
		/// </summary>
		/// <param name="values"></param>
		/// <param name="id"></param>
		/// <param name="name"></param>
		/// <param name="callback"></param>
		public void Share(Dictionary<string, object> values, string id = "", string name = "",
			Action<UserStatistics> callback = null) => ensureAccessToken(() => share(values, id, name, callback), () => callback?.Invoke(null));

		private void share(Dictionary<string, object> values, string id = "", string name = "", Action<UserStatistics> callback = null) {
			this.DebugLogNoContext($"Globalstats.io: Submitting values with ID = {StatisticId ?? "<null>"}");
			// If no id is supplied but we have one stored, reuse it.
			if (string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(StatisticId)) {
				id = StatisticId;
			}

			var update = false;
			if (!string.IsNullOrEmpty(id)) {
				update = true;
			} else {
				if (string.IsNullOrWhiteSpace(name)) {
					name = "Anonymous";
				}
			}

			string jsonPayload;

			if (update == false) {
				jsonPayload = "{\"name\":\"" + name + "\", \"values\":";
			} else {
				jsonPayload = "{\"values\":";
			}

			jsonPayload += values.AsJsonString();
			jsonPayload += "}";

			if (update) {
				this.DebugLogNoContext($"Globalstats.io: Updating values: {jsonPayload}");
				restClient.Put<StatisticResponse>("v1/statistics", id, jsonPayload,
					response => {
						handleShareResponse(response, callback);
					});
			} else {
				this.DebugLogNoContext($"Globalstats.io: Posting values for the first time: {jsonPayload}");
				restClient.Post<StatisticResponse>("v1/statistics",
					jsonPayload,
					response => {
						handleShareResponse(response, callback);
					});
			}
		}

		private void handleShareResponse(RestResponse<StatisticResponse> response, Action<UserStatistics> callback = null) {
			if (!response.IsSuccess) {
				Debug.LogWarning("Error submitting statistic: " + response.ErrorMessage);
				Debug.Log("GlobalstatsIO API Response: " + response.AdditionalInfo);
				callback?.Invoke(null);
				return;
			}
			var statistic = response.Data;

			// ID is available only on create, not on update, so do not overwrite it
			if (!string.IsNullOrEmpty(statistic?._id)) StatisticId = statistic._id;

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

			callback?.Invoke(response.Data.ToUserStatistics());
		}
		#endregion

		#region GetStatistics
		public void GetStatistics(Action<UserStatistics> onResponse) {
			if (StatisticId.IsNullOrEmpty()) {
				onResponse(null);
				return;
			}

			ensureAccessToken(() => {
					restClient.Get<UserStatistics>("v1/statistics",
						StatisticId,
						response => {
							onResponse(response.IsSuccess ? response.Data : null);
						});
				},
				() => {
					onResponse(null);
				});
		}
		#endregion

		public IEnumerator LinkStatistic(string id = "", Action<bool> callback = null) {
			if (!restClient.HasValidAccessToken) {
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
				www.SetRequestHeader("Authorization", "Bearer " + restClient.AuthToken.access_token);
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

			if (!restClient.HasValidAccessToken) {
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
				www.SetRequestHeader("Authorization", "Bearer " + restClient.AuthToken.access_token);
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