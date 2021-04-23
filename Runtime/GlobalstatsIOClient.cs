using CommonUtils;
using CommonUtils.RestSdk;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace GlobalstatsIO {
	public class GlobalstatsIOClient {
		private const string STATISTIC_ID_PREF_KEY = "globalstats.io.statistic-id";
		private const string BASE_URL = "https://api.globalstats.io";

		#region Singleton
		private static GlobalstatsIOClient instance;
		public static GlobalstatsIOClient Instance { get; private set; }

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

		#region Serializable classes
		[Serializable]
		private class StatisticResponse {
			public string name = null;
			public string _id = null;

			[SerializeField]
			public List<StatisticValues> values = null;
		}
		#endregion

		private void ensureAccessToken(Action onSuccess = null, Action onError = null) {
			if (restClient.HasValidAccessToken) {
				onSuccess?.Invoke();
				return;
			}
			restClient.Post<AccessToken>("oauth/access_token",
				new Dictionary<string, object>() {
					{"grant_type", "client_credentials"},
					{"scope", "endpoint_client"},
					{"client_id", ApiConfig.Instance.GlobalstatsId},
					{"client_secret", ApiConfig.Instance.GlobalstatsSecret}
				},
				response => {
					if (response.IsSuccess) {
						restClient.SetAccessToken(response.Data);
						onSuccess?.Invoke();
					} else {
						onError?.Invoke();
					}
				});
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
					restClient.SetAccessToken(JsonUtility.FromJson<AccessToken>(responseBody));
				}
			}
		}

		/// <summary>
		/// Asynchronously submits the specified <paramref name="values"/>.
		/// </summary>
		/// <param name="values"></param>
		/// <param name="id"></param>
		/// <param name="name"></param>
		/// <param name="callback"></param>
		public void Share(Dictionary<string, string> values, string id = "", string name = "",
			Action<bool> callback = null) => ensureAccessToken(() => share(values, id, name, callback));

		private void share(Dictionary<string, string> values, string id = "", string name = "", Action<bool> callback = null) {
			// If no id is supplied but we have one stored, reuse it.
			if (string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(StatisticId)) {
				id = StatisticId;
			}

			var update = false;
			if (!string.IsNullOrEmpty(id)) {
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

			if (update) {
				restClient.Put<StatisticResponse>("v1/statistics", id, jsonPayload,
					response => {
						handleShareResponse(response, callback);
					});
			} else {
				restClient.Post<StatisticResponse>("v1/statistics",
					jsonPayload,
					response => {
						handleShareResponse(response, callback);
					});
			}
		}

		private void handleShareResponse(RestResponse<StatisticResponse> response, Action<bool> callback = null) {
			if (!response.IsSuccess) {
				Debug.LogWarning("Error submitting statistic: " + response.ErrorMessage);
				Debug.Log("GlobalstatsIO API Response: " + response.AdditionalInfo);
				callback?.Invoke(false);
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