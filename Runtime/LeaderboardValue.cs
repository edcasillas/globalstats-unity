using System;

namespace GlobalstatsIO {
	[Serializable]
	public class LeaderboardValue {
		public string name = null;
		public string user_profile = null;
		public string user_icon = null;
		public string rank = "0";
		public string value = "0";

		/// <summary>
		/// Indicates whether this leaderboard value belongs to the current player.
		/// </summary>
		public bool IsSelf { get; set; }
	}
}