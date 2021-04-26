using UnityEngine;

namespace GlobalstatsIO {
	public class ApiConfig : ScriptableObject {
		/// <summary>
		/// Path to the asset, under the Assets/Resources folder.
		/// </summary>
		public const string AssetPath = "Config";
		public const string AssetName = "GlobalstatsIO";

		public static string FullRelativeName => $"{AssetPath}/{AssetName}";

		#region Singleton definition
		private static ApiConfig instance;
		public static ApiConfig Instance {
			get {
				if (!instance) Init();
				return instance;
			}
			private set => instance = value;
		}

		private ApiConfig() { }
		#endregion

		private static bool isInitialized;

		public static bool Init() {
			if (isInitialized) return true;

			Debug.Log($"Loading GlobalstatsIO API configuration from {FullRelativeName}");
			var cfg = Resources.Load<ApiConfig>(FullRelativeName);
			if (!cfg) {
				Debug.LogError("GlobalstatsIO API configuration asset has not been created. It might be missing from the Resources folder (in Android, this can happen if the OBB file isn't present.)");
				return false;
			}

			Instance = cfg;
			isInitialized = true;
			return true;
		}

		#region Inspector editable fields
#pragma warning disable 649
		[SerializeField] private string globalstatsId;
		[SerializeField] private string globalstatsSecret;
		[SerializeField] private bool verbose;
#pragma warning restore 649
		#endregion

		public string GlobalstatsId => globalstatsId;
		public string GlobalstatsSecret => globalstatsSecret;
		public bool IsVerbose => verbose;
	}
}