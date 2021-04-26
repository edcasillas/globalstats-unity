using System;
using System.Collections.Generic;

namespace GlobalstatsIO {
	[Serializable]
	public class StatisticSection {
		public RanksData better_ranks;
		public LeaderboardValue user_rank;
		public RanksData worse_ranks;
	}

	[Serializable]
	public class RanksData {
		public List<LeaderboardValue> data;
	}
}