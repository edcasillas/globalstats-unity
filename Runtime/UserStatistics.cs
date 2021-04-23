using System;
using System.Collections.Generic;
using UnityEngine;

namespace GlobalstatsIO {
	[Serializable]
	public class UserStatistics {
		public string name = null;

		[SerializeField]
		public List<StatisticValues> statistics = null;
	}
}