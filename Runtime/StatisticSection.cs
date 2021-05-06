using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GlobalstatsIO {
	[Serializable]
	public class StatisticSection : IEnumerable<LeaderboardValue> {
		[SerializeField] private RanksData better_ranks;
		[SerializeField] private LeaderboardValue user_rank;
		[SerializeField] private RanksData worse_ranks;

		public RanksData BetterRanks => better_ranks;

		public LeaderboardValue UserRank {
			get {
				user_rank.IsSelf = true;
				return user_rank;
			}
		}

		public RanksData WorseRanks => worse_ranks;

		public IEnumerator<LeaderboardValue> GetEnumerator() {
			if (better_ranks?.data != null) {
				foreach (var entry in better_ranks.data) {
					yield return entry;
				}
			}

			if (user_rank != null) {
				yield return UserRank;
			}

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