#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

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
			Console.WriteLine("Parsing and requesting for geospatial information");
			await Amender.PatchUnitInfo(overrideStream.OverrideEntries(ParseAndRequestUnit(serviceUrl,
					overrideStream, executablePath, client)), executablePath);
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
			await overrideStream.ReadLocatedEntries(locator, new LocatingObserver());
			// Console.WriteLine("Creating address override table");
			return overrideStream;
		}

		private static async IAsyncEnumerator<UnitInformationEntry> ParseAndRequestUnit(string url,
				CoordinatesOverrideStream overrides, string executablePath, HttpClient client)
		{
			UnitInformationStream informationStream = new(mode => throw new Exception("placeholder"));
			IAsyncEnumerator<UnitInformationEntry> unlocatedEntries = informationStream.ReadEntriesFromUrl(url, client);
			NetworkUtility.AddressLocator locator = address => AlsLocationInfo.FromAddress(
					address, true, client);
			IAsyncEnumerator<UnitInformationEntry> locatedEntries = unlocatedEntries
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
