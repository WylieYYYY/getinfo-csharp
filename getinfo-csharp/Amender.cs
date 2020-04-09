using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;

namespace getinfo_csharp
{
	static class Amender
	{
		public static async Task AmendUnitInfo(Dictionary<string, string> overrideTable, string executablePath)
		{
			JsonElement longlatElement, unitElement;
			(longlatElement, unitElement) = ParseUnitInfo(executablePath);
			Dictionary<string, Tuple<float, float>> longlatOverrideTable;
			Dictionary<string, string> addressTable;
			(longlatOverrideTable, addressTable) = await RequestOverrideLonglat(overrideTable);
			Tuple<float, float>[] longlatList;
			IEnumerable<string> unitJsonList = unitElement.EnumerateArray().Skip(1).Select(e => e.GetRawText());
			string[][] unitList = unitJsonList.Select(s => JsonSerializer.Deserialize<object[]>(s)
				.Select(o => o?.ToString()).ToArray()).ToArray();
			longlatList = PatchLists(longlatElement, unitElement, longlatOverrideTable, addressTable, ref unitList);
			PatchUnitInfo(longlatList, unitElement[0].EnumerateArray()
				.Select(j => j.GetString()).ToArray(), unitList, executablePath);
		}

		private static (JsonElement, JsonElement) ParseUnitInfo(string executablePath)
		{
			string longlat = "", unitinfo = "";
			using (StreamReader stream = new StreamReader(executablePath + "/../scripts/unitinfo.js"))
			{
				string line;
				while ((line = stream.ReadLine()) != null)
				{
					Match match = Regex.Match(line, @"^var longlat ?= ?(.*);$");
					if (match.Success) longlat = match.Groups[1].Value;
					match = Regex.Match(line, @"^var unitinfo ?= ?(.*);$");
					if (match.Success) unitinfo = match.Groups[1].Value;
				}
			}
			return (JsonDocument.Parse(longlat).RootElement, JsonDocument.Parse(unitinfo).RootElement);
		}

		public static async Task<(Dictionary<string, Tuple<float, float>>, Dictionary<string, string>)>RequestOverrideLonglat
			(Dictionary<string, string> overrideTable)
		{
			Console.WriteLine("Requesting override table");
			IEnumerable<string> requestGen = overrideTable.Where(
				kv => kv.Value.Split('\t')[0] != "[NO OVERRIDE]")
				.Select(kv => "https://www.als.ogcio.gov.hk/lookup?n=1&q=" + HttpUtility.UrlEncode(
				overrideTable[kv.Key].Split('\t')[0]) + "&i=" + HttpUtility.UrlEncode(kv.Key));
			Dictionary<string, Tuple<float, float>> longlatOverrideTable =
				new Dictionary<string, Tuple<float, float>>();
			Dictionary<string, string> addressTable = new Dictionary<string, string>();
			Program.estimatePacer = new Pacer(requestGen.Count());
			Task<(string, string, XDocument, float, float)>[] taskResponse;
			for (int batchStart = 0; batchStart < requestGen.Count(); batchStart += 50)
			{
				taskResponse = requestGen.Skip(batchStart).Take(50).Select(s => Program.LonglatProcess(s)).ToArray();
				await Task.WhenAll(taskResponse);
				foreach ((string requestUrl, _, _, float lon, float lat) in taskResponse.Select(t => t.Result))
				{
					string nameKey = HttpUtility.UrlDecode(requestUrl.Split("&i=")[1]);
					string[] offset = overrideTable[nameKey].Split('\t').Skip(1).Take(2).ToArray();
					longlatOverrideTable.Add(nameKey, new Tuple<float, float>(
						lon + float.Parse(offset[0]), lat + float.Parse(offset[1])));
					addressTable.Add(nameKey, HttpUtility.UrlDecode(requestUrl.Split("&i")[0].Split("&q=")[1]));
				}
			}
			Program.estimatePacer.Stop();
			return (longlatOverrideTable, addressTable);
		}

		private static Tuple<float, float>[] PatchLists(JsonElement longlatElement, JsonElement unitElement,
			Dictionary<string, Tuple<float, float>> longlatOverrideTable,
			Dictionary<string, string> addressTable, ref string[][] unitList)
		{
			Console.WriteLine("Patching unit info lists");
			int nameKeyIndex = Array.IndexOf(unitElement[0].EnumerateArray().Select(k => k.GetString()).ToArray(), "nameTChinese");
			int addressKeyIndex = Array.IndexOf(unitElement[0].EnumerateArray()
				.Select(k => k.GetString()).ToArray(), "addressOverride");
			IEnumerable<string> longlatJsonList = longlatElement.EnumerateArray().Select(e => e.GetRawText());
			IEnumerable<float[]> longlatArrayList = longlatJsonList.Select(s => JsonSerializer.Deserialize<float[]>(s));
			Tuple<float, float>[] longlatList = longlatArrayList.Select(l => new Tuple<float, float>(l[0], l[1])).ToArray();
			string[] nameKeyList = unitElement.EnumerateArray().Skip(1).Select(u => u[nameKeyIndex].GetString()).ToArray();
			foreach (KeyValuePair<string, Tuple<float, float>> kv in longlatOverrideTable)
			{
				int unitIndex = Array.IndexOf(nameKeyList, kv.Key);
				longlatList[unitIndex] = kv.Value;
				unitList[unitIndex][addressKeyIndex] = addressTable[kv.Key];
			}
			return longlatList;
		}

		public static void PatchUnitInfo(Tuple<float, float>[] longlatList, IEnumerable<string> infoKeyList,
			string[][] unitList, string executablePath)
		{
			Console.WriteLine("Reordering data by decreasing latitude");
			IEnumerable<(int, Tuple<float, float>)> zip = Enumerable.Range(0, longlatList.Length).Zip(longlatList);
			zip = zip.OrderByDescending(kv => kv.Item2.Item2);
			int[] latUnitMap = zip.Select(kv => kv.Item1).ToArray();
			longlatList = zip.Select(kv => kv.Item2).ToArray();
			// uses JS to avoid CORS
			Console.WriteLine("Writing langlat and dumping XML as JS to unitinfo.js");
			using (StreamWriter stream = new StreamWriter(executablePath +
				"/../scripts/unitinfo.js", false, Encoding.UTF8))
			{
				stream.Write("var longlat = [");
				bool firstWrite = true;
				foreach (Tuple<float, float> longlat in longlatList)
				{
					if (!firstWrite) stream.Write(',');
					stream.Write($"[{longlat.Item1},{longlat.Item2}]");
					firstWrite = false;
				}
				stream.Write("];\nvar unitinfo = [[");
				stream.Write(string.Join(',', infoKeyList.Select(k => $"\"{k}\"")));
				stream.Write(']');
				foreach (int latIndex in latUnitMap)
				{
					stream.Write(",[");
					stream.Write(string.Join(',', unitList[latIndex]
						.Select(v => v == "" || v == null ? "null" : $"\"{v}\"")));
					stream.Write(']');
				}
				stream.Write("];");
			}
			Console.WriteLine("Finished unitinfo.js");
		}
	}
}
