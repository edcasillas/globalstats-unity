using System;
using System.Collections;
using System.Collections.Generic;

namespace GlobalstatsIO {
	[Serializable]
	public class StatisticSection : IEnumerable<LeaderboardValue> {
		public RanksData better_ranks;
		public LeaderboardValue user_rank;
		public RanksData worse_ranks;

		public IEnumerator<LeaderboardValue> GetEnumerator() {
			if (better_ranks?.data != null) {
				foreach (var entry in better_ranks.data) {
					yield return entry;
				}
			}

			if (user_rank != null) yield return user_rank;

			if (worse_ranks?.data != null) {
				foreach (var entry in worse_ranks.data) {
					yield return entry;
				}
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	[Serializable]
	public class RanksData {
		public List<LeaderboardValue> data;
	}
}