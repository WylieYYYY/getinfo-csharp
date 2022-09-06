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
		public static async Task AmendUnitInfo(CoordinatesOverrideStream overrideStream, string executablePath)
		{
			IAsyncEnumerator<UnitInformationEntry> unitInformationEntries = ReadUnitInfo(executablePath);
			Console.WriteLine("Patching unit info lists");
			IAsyncEnumerator<UnitInformationEntry> patchedUnitInformationEntries =
					overrideStream.OverrideEntries(unitInformationEntries);
			await PatchUnitInfo(patchedUnitInformationEntries, executablePath);
		}

		private static async IAsyncEnumerator<UnitInformationEntry> ReadUnitInfo(string executablePath)
		{
			string longlatJson = "", unitinfoJson = "";
			using (StreamReader stream = new StreamReader(executablePath + "/../scripts/unitinfo.js"))
			{
				string line;
				while ((line = stream.ReadLine()) != null)
				{
					Match match = Regex.Match(line, @"^var longlat ?= ?(.*);$");
					if (match.Success) longlatJson = match.Groups[1].Value;
					match = Regex.Match(line, @"^var unitinfo ?= ?(.*);$");
					if (match.Success) unitinfoJson = match.Groups[1].Value;
				}
			}
			JsonElement longlatElement = JsonDocument.Parse(longlatJson).RootElement;
			JsonElement unitinfoElement = JsonDocument.Parse(unitinfoJson).RootElement;
			UnitInformationEntry.SharedAttributeKeys = unitinfoElement[0].Deserialize<string[]>();
			IEnumerable<string?[]> unitList = unitinfoElement.EnumerateArray().Skip(1).Select(e => e.Deserialize<string?[]>());
			IEnumerable<Vector2> longlatList = longlatElement.EnumerateArray()
					.Select(e => e.Deserialize<float[]>()).Select(l => new Vector2(l[0], l[1]));
			foreach ((string?[] unitinfo, Vector2 longlat) in unitList.Zip(longlatList))
			{
				Dictionary<string, string?> attributes = new();
				foreach ((string key, string? value) in UnitInformationEntry.SharedAttributeKeys.Zip(unitinfo))
					attributes.Add(key, value);
				yield return new UnitInformationEntry(attributes, longlat);
			}
		}

		public static async Task<CoordinatesOverrideStream> RequestOverrideLonglat(
				Dictionary<string, string> overrideTable)
		{
			Console.WriteLine("Requesting override table");
			List<CoordinatesOverrideEntry> overrides = overrideTable.Select(
					p => new CoordinatesOverrideEntry(p.Key, p.Value.Split('\t')[3], p.Value.Split('\t')[0],
					new Vector2(float.Parse(p.Value.Split('\t')[1]), float.Parse(p.Value.Split('\t')[2])))).ToList();
			Pacer estimatePacer = new(overrides.Count);
			Dictionary<object, CoordinatesOverrideEntry> overridesLookup = new();
			NetworkUtility.AddressLocator locator = address => AlsLocationInfo.FromAddress(
					address, false, Program.client);
			IAsyncEnumerator<CoordinatesOverrideEntry> asyncOverrides = overrides.GetEnumerator()
					.ToAsyncEnumerator().ChunkComplete(entry => entry.Locate(locator), Program.batchSize);
			while (await asyncOverrides.MoveNextAsync())
			{
				string estimatedTimeString = estimatePacer.Step().ToString();
				if (asyncOverrides.Current.ProposedAddress == null) continue;
				if (asyncOverrides.Current.OverridingCoordinates == null)
					Console.WriteLine($"Request failed for {asyncOverrides.Current.TraditionalChineseName}");
				else Console.WriteLine($"Got response for {asyncOverrides.Current.TraditionalChineseName}");
				Console.WriteLine($"[Estimated time left: {estimatedTimeString}]");
			}
			estimatePacer.Stop();
			CoordinatesOverrideStream coStream = new();
			coStream.PendingChanges = overridesLookup;
			return coStream;
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
