#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

using WylieYYYY.GetinfoCSharp;
using WylieYYYY.GetinfoCSharp.IO;
using WylieYYYY.GetinfoCSharp.Net;

namespace getinfo_csharp
{
	class Program
	{
		static async Task Main(string[] args)
		{
			const string CoordinatesOverridePath = "override.csv";
			const string UnitInformationPath = "scripts/unitinfo.js";
			Console.WriteLine(Resources.Messages.GenerateLocation(UnitInformationPath));
			Console.Write(Resources.Messages.InputCommand(CoordinatesOverridePath, UnitInformationPath));
			string serviceUrl = Console.ReadLine() ?? string.Empty;
			string executablePath = Path.GetDirectoryName(typeof(Program).Assembly.Location)!;
			using CoordinatesOverrideStream overrideStream = await GetOverrideStream(executablePath);
			if (serviceUrl == string.Empty) serviceUrl = overrideStream.SourceUrl!;
			if (serviceUrl.ToLower() == "amend")
			{
				await Amender.AmendUnitInfo(overrideStream, executablePath);
				Console.WriteLine(Resources.Messages.ExitSuccess);
				Console.ReadLine();
				return;
			}
			Console.WriteLine("XML request sent");
			// "XML request for \"{xmlUrl}\" HTTP error"
			XDocument serviceRoot = XDocument.Parse(await client.RequestTwiceOrBail(serviceUrl));
			Console.WriteLine("Parsing and requesting for geospatial information");
			await Amender.PatchUnitInfo(overrideStream.OverrideEntries(ParseAndRequestUnit(serviceRoot,
					overrideStream, executablePath)), executablePath);
			// Console.WriteLine("Applying override table");
			Console.WriteLine(Resources.Messages.ExitSuccess);
			Console.ReadLine();
		}

		public static readonly HttpClient client = new HttpClient();

		// create or read address override table
		private static async Task<CoordinatesOverrideStream> GetOverrideStream(string executablePath)
		{
			Console.WriteLine("Reading address override table");
			// Console.WriteLine("Requesting override table");
			CoordinatesOverrideStream overrideStream = new(new FileStream(
					executablePath + "/../override.csv", FileMode.OpenOrCreate));
			await overrideStream.ReadConfigurations();
			NetworkUtility.AddressLocator locator = address => AlsLocationInfo.FromAddress(
					address, false, client);
			IAsyncEnumerator<object> identifiers = overrideStream.ReadLocatedEntries(locator);
			// Pacer estimatePacer = new(overrides.Count);
			while (await identifiers.MoveNextAsync())
				// TimeSpan estimatedTimeLeft = estimatePacer.Step();
				Console.WriteLine(Resources.Messages.Located(identifiers.Current.ToString()!));
				// Console.WriteLine(Resources.Messages.TimeEstimation(estimatedTimeLeft));
			// Console.WriteLine("Creating address override table");
			return overrideStream;
		}

		private static async IAsyncEnumerator<UnitInformationEntry> ParseAndRequestUnit(XDocument root,
				CoordinatesOverrideStream overrides, string executablePath)
		{
			XNamespace xmlnsUrl = root.Root.GetDefaultNamespace();
			IEnumerator<UnitInformationEntry> GetEntriesFromXml()
			{
				foreach (XElement unit in root.Descendants(xmlnsUrl + "serviceUnit"))
				{
					Dictionary<string, string?> propDict = new();
					foreach (XElement prop in unit.Descendants())
						propDict.Add(prop.Name.LocalName, prop.Value);
					propDict.Add("addressOverride", null);
					UnitInformationEntry.SharedAttributeKeys = propDict.Keys.ToArray();
					yield return new UnitInformationEntry(propDict, UnitInformationEntry.MissingCoordinates);
				}
			}
			NetworkUtility.AddressLocator locator = address => AlsLocationInfo.FromAddress(
					address, true, client);
			IAsyncEnumerator<UnitInformationEntry> locatedEntries = GetEntriesFromXml().ToAsyncEnumerator()
					.ChunkComplete(entry => entry.Locate(locator), overrides.BatchSize);
			while (await locatedEntries.MoveNextAsync())
			{
				string name = locatedEntries.Current["nameTChinese"].ToUpperInvariant();
				if (locatedEntries.Current.Coordinates == UnitInformationEntry.MissingCoordinates)
				{
					string address = locatedEntries.Current["addressTChinese"];
					await overrides.WriteEntry(new CoordinatesOverrideEntry(name, address));
					Console.WriteLine(Resources.Messages.FailedToLocate(name));
				}
				else Console.WriteLine(Resources.Messages.Located(name));
				// Console.WriteLine("[Estimated time left: " + estimatePacer.Step().ToString() + ']');
				yield return locatedEntries.Current;
			}
			// Console.WriteLine($"Query ratio is {ratio.Item1}:{ratio.Item2}");
		}
	}
}
