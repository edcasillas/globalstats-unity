using CommonUtils;
using CommonUtils.Editor;
using UnityEditor;

namespace GlobalstatsIO.Editor {
	public static class MenuItems {
		[MenuItem("Tools/Configure GlobalstatsIO...")]
		private static void configure() {
			if (!EditorUtils.HighlightAssetOfType<ApiConfig>(ApiConfig.FullRelativeName)) {
				ScriptableObjectUtility.CreateAsset<ApiConfig>(ApiConfig.AssetName, $"Assets/Resources/{ApiConfig.AssetPath}", true);
			}
		}
	}
}