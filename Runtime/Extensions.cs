using System.Collections.Generic;
using System.Linq;

namespace GlobalstatsIO {
	public static class Extensions {
		public static StatisticValues FindByKey(this IEnumerable<StatisticValues> values, string key)
			=> values.FirstOrDefault(t => t.key == key);

		public static Dictionary<string, StatisticValues> ToDictionary(this List<StatisticValues> values)
			=> values.ToDictionary(s => s.key, s => s);
	}
}