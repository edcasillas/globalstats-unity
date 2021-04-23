using System.Collections.Generic;

namespace GlobalstatsIO {
	public static class Extensions {
		public static StatisticValues FindByKey(this List<StatisticValues> values, string key) {
			foreach (var t in values) {
				if (t.key == key) {
					return t;
				}
			}

			return null;
		}
	}
}