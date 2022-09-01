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
using WylieYYYY.GetinfoCSharp.IO;
using WylieYYYY.GetinfoCSharp.Net;

namespace getinfo_csharp
{
	static class Amender
	{
		public static async Task AmendUnitInfo(Dictionary<string, string> overrideTable, string executablePath)
		{
			(JsonElement longlatElement, JsonElement unitElement) = ParseUnitInfo(executablePath);
			IAsyncEnumerator<CoordinatesOverrideEntry> overrides = RequestOverrideLonglat(overrideTable);
			(Vector2[] longlatList, string?[][] unitList) =
					await PatchLists(longlatElement, unitElement, overrides);
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

		public static async IAsyncEnumerator<CoordinatesOverrideEntry> RequestOverrideLonglat(
				Dictionary<string, string> overrideTable)
		{
			Console.WriteLine("Requesting override table");
			List<CoordinatesOverrideEntry> overrides = overrideTable.Select(
					p => new CoordinatesOverrideEntry(p.Key, p.Value.Split('\t')[3], p.Value.Split('\t')[0],
					new Vector2(float.Parse(p.Value.Split('\t')[1]), float.Parse(p.Value.Split('\t')[2])))).ToList();
			IEnumerator<CoordinatesOverrideEntry> overrideEnumerator = overrides.GetEnumerator();
			Pacer estimatePacer = new(overrides.Count);
			List<Task<CoordinatesOverrideEntry>> tasks = new();
			async IAsyncEnumerator<CoordinatesOverrideEntry> RequestChunk()
			{
				IAsyncEnumerator<CoordinatesOverrideEntry> chunk = tasks.UnrollCompletedTasks();
				while (await chunk.MoveNextAsync())
				{
					if (chunk.Current.ProposedAddress == null)
					{
						estimatePacer.Step();
						continue;
					}
					if (chunk.Current.OverridingCoordinates == null)
						Console.WriteLine($"Request failed for {chunk.Current.TraditionalChineseName}");
					else Console.WriteLine($"Got response for {chunk.Current.TraditionalChineseName}");
					Console.WriteLine($"[Estimated time left: {estimatePacer.Step().ToString()}]");
					if (chunk.Current.OverridingCoordinates != null) yield return chunk.Current;
				}
			}
			while (overrideEnumerator.MoveNext())
			{
				tasks.Add(overrideEnumerator.Current.Locate(
						address => AlsLocationInfo.FromAddress(address, false, Program.client)));
				if (tasks.Count == Program.batchSize)
				{
					IAsyncEnumerator<CoordinatesOverrideEntry> chunk = RequestChunk();
					while (await chunk.MoveNextAsync()) yield return chunk.Current;
				}
			}
			IAsyncEnumerator<CoordinatesOverrideEntry> lastChunk = RequestChunk();
			while (await lastChunk.MoveNextAsync()) yield return lastChunk.Current;
			estimatePacer.Stop();
		}

		private static async Task<(Vector2[], string?[][])> PatchLists(JsonElement longlatElement,
				JsonElement unitElement, IAsyncEnumerator<CoordinatesOverrideEntry> overrides)
		{
			Console.WriteLine("Patching unit info lists");
			string?[][] unitList = unitElement.EnumerateArray().Skip(1).Select(e => e.Deserialize<string?[]>()).ToArray();
			string?[] keys = unitElement[0].Deserialize<string?[]>();
			int nameKeyIndex = Array.IndexOf(keys, "nameTChinese");
			int addressKeyIndex = Array.IndexOf(keys, "addressOverride");
			IEnumerable<float[]> longlatArrayList = longlatElement.EnumerateArray().Select(e => e.Deserialize<float[]>());
			Vector2[] longlatList = longlatArrayList.Select(l => new Vector2(l[0], l[1])).ToArray();
			string[] nameKeyList = unitElement.EnumerateArray().Skip(1).Select(u => u[nameKeyIndex].GetString()).ToArray();
			while (await overrides.MoveNextAsync())
			{
				int unitIndex = Array.IndexOf(nameKeyList, overrides.Current.TraditionalChineseName);
				if (unitIndex == -1) continue;
				longlatList[unitIndex] = (Vector2)overrides.Current.OverridingCoordinates!;
				unitList[unitIndex][addressKeyIndex] = overrides.Current.ProposedAddress;
			}
			return (longlatList, unitList);
		}

		public static void PatchUnitInfo(Vector2[] longlatList, IEnumerable<string> infoKeyList,
			string[][] unitList, string executablePath)
		{
			Console.WriteLine("Reordering data by decreasing latitude");
			IEnumerable<(int, Vector2)> zip = Enumerable.Range(0, longlatList.Length).Zip(longlatList);
			zip = zip.OrderByDescending(kv => kv.Item2.Y);
			int[] latUnitMap = zip.Select(kv => kv.Item1).ToArray();
			longlatList = zip.Select(kv => kv.Item2).ToArray();
			// uses JS to avoid CORS
			Console.WriteLine("Writing langlat and dumping XML as JS to unitinfo.js");
			using (StreamWriter stream = new StreamWriter(executablePath +
				"/../scripts/unitinfo.js", false, Encoding.UTF8))
			{
				stream.Write("var longlat = ");
				stream.Write(JsonSerializer.Serialize(longlatList.Select(c => new float[] { c.X, c.Y })));
				stream.Write(";\nvar unitinfo = [");
				stream.Write(JsonSerializer.Serialize(infoKeyList));
				foreach (int latIndex in latUnitMap)
				{
					stream.Write(',');
					stream.Write(JsonSerializer.Serialize(unitList[latIndex]));
				}
				stream.Write("];");
			}
			Console.WriteLine("Finished unitinfo.js");
		}
	}
}
