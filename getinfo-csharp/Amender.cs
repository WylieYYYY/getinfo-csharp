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
			IAsyncEnumerator<UnitInformationEntry> unitInformationEntry =
					PatchLists(longlatElement, unitElement, overrides);
			await PatchUnitInfo(unitInformationEntry, executablePath);
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

		private static async IAsyncEnumerator<UnitInformationEntry> PatchLists(
				JsonElement longlatElement, JsonElement unitElement,
				IAsyncEnumerator<CoordinatesOverrideEntry> overrides)
		{
			Console.WriteLine("Patching unit info lists");
			Dictionary<string, CoordinatesOverrideEntry> overridesLookup = new();
			while (await overrides.MoveNextAsync())
				overridesLookup.Add(overrides.Current.TraditionalChineseName, overrides.Current);
			UnitInformationEntry.SharedAttributeKeys = unitElement[0].Deserialize<string[]>();
			IEnumerable<string?[]> unitList = unitElement.EnumerateArray().Skip(1).Select(e => e.Deserialize<string?[]>());
			int nameKeyIndex = Array.IndexOf(UnitInformationEntry.SharedAttributeKeys, "nameTChinese");
			int addressKeyIndex = Array.IndexOf(UnitInformationEntry.SharedAttributeKeys, "addressOverride");
			IEnumerable<Vector2> longlatList = longlatElement.EnumerateArray()
					.Select(e => e.Deserialize<float[]>()).Select(l => new Vector2(l[0], l[1]));
			foreach ((string?[] attributeValues, Vector2 coordinates) in unitList.Zip(longlatList))
			{
				Dictionary<string, string?> attributes = new();
				foreach ((string key, string? value) in UnitInformationEntry.SharedAttributeKeys.Zip(attributeValues))
					attributes.Add(key, value);
				Vector2? overridenCoordinates = coordinates;
				if (overridesLookup.ContainsKey(attributes["nameTChinese"]))
				{
					CoordinatesOverrideEntry overrideEntry = overridesLookup[attributes["nameTChinese"]];
					overridenCoordinates = overrideEntry.OverridingCoordinates;
					attributeValues[addressKeyIndex] = overrideEntry.ProposedAddress;
				}
				yield return new UnitInformationEntry(attributes, (Vector2)overridenCoordinates);
			}
		}

		public static async Task PatchUnitInfo(IAsyncEnumerator<UnitInformationEntry> entries,
				string executablePath)
		{
			Console.WriteLine("Reordering data by decreasing latitude");
			SortedSet<UnitInformationEntry> entrySet = new();
			while (await entries.MoveNextAsync()) entrySet.Add(entries.Current);
			// uses JS to avoid CORS
			Console.WriteLine("Writing langlat and dumping XML as JS to unitinfo.js");
			using StreamWriter stream = new(executablePath + "/../scripts/unitinfo.js", false, Encoding.UTF8);
			stream.Write("var longlat = ");
			stream.Write(JsonSerializer.Serialize(entrySet.Select(
					c => new float[] { c.Coordinates.X, c.Coordinates.Y })));
			stream.Write(";\nvar unitinfo = [");
			string[] attributeKeys = UnitInformationEntry.SharedAttributeKeys;
			stream.Write(JsonSerializer.Serialize<string[]>(attributeKeys));
			foreach (UnitInformationEntry entry in entrySet)
			{
				stream.Write(',');
				stream.Write(JsonSerializer.Serialize(attributeKeys.Select(k => entry[k])));
			}
			stream.Write("];");
			Console.WriteLine("Finished unitinfo.js");
		}
	}
}
