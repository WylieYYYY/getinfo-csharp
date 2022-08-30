#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using WylieYYYY.GetinfoCSharp;
using WylieYYYY.GetinfoCSharp.Net;

namespace getinfo_csharp
{
	static class Amender
	{
		public static async Task AmendUnitInfo(Dictionary<string, string> overrideTable, string executablePath)
		{
			JsonElement longlatElement, unitElement;
			(longlatElement, unitElement) = ParseUnitInfo(executablePath);
			Dictionary<string, Vector2> longlatOverrideTable;
			Dictionary<string, string> addressTable;
			(longlatOverrideTable, addressTable) = await RequestOverrideLonglat(overrideTable);
			Vector2[] longlatList;
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

		public static async Task<(Dictionary<string, Vector2>, Dictionary<string, string>)>RequestOverrideLonglat
			(Dictionary<string, string> overrideTable)
		{
			Console.WriteLine("Requesting override table");
			IEnumerable<KeyValuePair<string, string>> keyAddressPairs = overrideTable
					.Where(kv => kv.Value.Split('\t')[0] != "[NO OVERRIDE]");
			Dictionary<string, Vector2> longlatOverrideTable = new();
			Dictionary<string, string> addressTable = new();
			for (int batchStart = 0; batchStart <  keyAddressPairs.Count(); batchStart += Program.batchSize)
			{
				List<Task<(string, AlsLocationInfo?)>> taskResponse = keyAddressPairs.Skip(batchStart).Take(Program.batchSize)
					.Select(async kv => (kv.Key, await AlsLocationInfo.FromAddress(kv.Value, false, Program.client))).ToList();
				IAsyncEnumerator<(string, AlsLocationInfo?)> taskEnumerator = taskResponse.UnrollCompletedTasks();
				Pacer estimatePacer = new(taskResponse.Count);
				while (await taskEnumerator.MoveNextAsync())
				{
					(string tckey, AlsLocationInfo? locationInfo) = taskEnumerator.Current;
					if (locationInfo == null)
					{
						Console.WriteLine("Request failed for " + tckey);
						continue;
					}
					string[] offset = overrideTable[tckey].Split('\t').Skip(1).Take(2).ToArray();
					longlatOverrideTable.Add(tckey, new Vector2(
						locationInfo.Coordinates.X + float.Parse(offset[0]),
						locationInfo.Coordinates.Y + float.Parse(offset[1])));
					addressTable.Add(tckey, locationInfo.SourceAddress);
					Console.WriteLine("Got response for " + tckey);
					Console.WriteLine("[Estimated time left: " + estimatePacer.Step().ToString() + ']');
				}
				estimatePacer.Stop();
			}
			return (longlatOverrideTable, addressTable);
		}

		private static Vector2[] PatchLists(JsonElement longlatElement, JsonElement unitElement,
			Dictionary<string, Vector2> longlatOverrideTable,
			Dictionary<string, string> addressTable, ref string[][] unitList)
		{
			Console.WriteLine("Patching unit info lists");
			int nameKeyIndex = Array.IndexOf(unitElement[0].EnumerateArray().Select(k => k.GetString()).ToArray(), "nameTChinese");
			int addressKeyIndex = Array.IndexOf(unitElement[0].EnumerateArray()
				.Select(k => k.GetString()).ToArray(), "addressOverride");
			IEnumerable<string> longlatJsonList = longlatElement.EnumerateArray().Select(e => e.GetRawText());
			IEnumerable<float[]> longlatArrayList = longlatJsonList.Select(s => JsonSerializer.Deserialize<float[]>(s));
			Vector2[] longlatList = longlatArrayList.Select(l => new Vector2(l[0], l[1])).ToArray();
			string[] nameKeyList = unitElement.EnumerateArray().Skip(1).Select(u => u[nameKeyIndex].GetString()).ToArray();
			foreach (KeyValuePair<string, Vector2> kv in longlatOverrideTable)
			{
				int unitIndex = Array.IndexOf(nameKeyList, kv.Key);
				if (unitIndex == -1) continue;
				longlatList[unitIndex] = kv.Value;
				unitList[unitIndex][addressKeyIndex] = addressTable[kv.Key];
			}
			return longlatList;
		}

		public static void PatchUnitInfo(Vector2[] longlatList, IEnumerable<string> infoKeyList,
			string[][] unitList, string executablePath)
		{
			Console.WriteLine("Reordering data by decreasing latitude");
			IEnumerable<(int, Vector2)> zip = Enumerable.Range(0, longlatList.Length).Zip(longlatList,
				(index, longlat) => (index, longlat));
			zip = zip.OrderByDescending(kv => kv.Item2.Y);
			int[] latUnitMap = zip.Select(kv => kv.Item1).ToArray();
			longlatList = zip.Select(kv => kv.Item2).ToArray();
			// uses JS to avoid CORS
			Console.WriteLine("Writing langlat and dumping XML as JS to unitinfo.js");
			using (StreamWriter stream = new StreamWriter(executablePath +
				"/../scripts/unitinfo.js", false, Encoding.UTF8))
			{
				stream.Write("var longlat = [");
				bool firstWrite = true;
				foreach (Vector2 longlat in longlatList)
				{
					if (!firstWrite) stream.Write(',');
					stream.Write($"[{longlat.X},{longlat.Y}]");
					firstWrite = false;
				}
				stream.Write("];\nvar unitinfo = [[");
				stream.Write(string.Join(',', infoKeyList.Select(k => $"\"{k}\"")));
				stream.Write(']');
				foreach (int latIndex in latUnitMap)
				{
					stream.Write(",[");
					stream.Write(string.Join(',', unitList[latIndex]
						.Select(v => v == "" || v == null ? "null" : $"\"{v.Replace("\"", "")}\"")));
					stream.Write(']');
				}
				stream.Write("];");
			}
			Console.WriteLine("Finished unitinfo.js");
		}
	}
}
