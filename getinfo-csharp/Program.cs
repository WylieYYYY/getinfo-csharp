﻿#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
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
			(serviceUrl, CoordinatesOverrideStream overrideStream) = await GetOverrideStream(serviceUrl, executablePath);
			if (serviceUrl.ToLower() == "amend")
			{
				await Amender.AmendUnitInfo(overrideStream, executablePath);
				Console.WriteLine(Resources.Messages.ExitSuccess);
				Console.ReadLine();
				return;
			}
			Console.WriteLine("XML request sent");
			// "XML request for \"{xmlUrl}\" HTTP error"
			XDocument serviceRoot = XDocument.Parse(await NetworkUtility.RequestTwiceOrBail(client, serviceUrl));
			XNamespace xmlnsUrl = Regex.Match(serviceRoot.Document.Root.Name.ToString(),
				@"(?<={)(https?://[^}]*)(?=})").Value;
			Console.WriteLine("Parsing and requesting for geospatial information");
			await Amender.PatchUnitInfo(overrideStream.OverrideEntries(ParseAndRequestUnit(serviceRoot, xmlnsUrl,
					overrideStream, executablePath)), executablePath);
			// Console.WriteLine("Applying override table");
			Console.WriteLine(Resources.Messages.ExitSuccess);
			Console.ReadLine();
		}

		public static readonly HttpClient client = new HttpClient();

		// create or read address override table
		public static int batchSize = 50;
		private static async Task<(string, CoordinatesOverrideStream)> GetOverrideStream(string xmlUrl,
			string executablePath)
		{
			Dictionary<string, string> table = new Dictionary<string, string>();
			if (File.Exists(executablePath + "/../override.csv"))
			{
				Console.WriteLine("Reading address override table");
				StreamReader stream = new StreamReader(executablePath + "/../override.csv", Encoding.UTF8);
				while (!stream.EndOfStream)
				{
					string[] fields = stream.ReadLine().Split('\t');
					if (fields[0] == "xml_url" && xmlUrl == "") xmlUrl = fields[1].TrimEnd();
					if (fields[0] == "batch_size") batchSize = int.Parse(fields[1].TrimEnd());
					// ignore any entry key with lowercase character and empty line
					if (fields[0].Any(char.IsLower) || fields[0] == "") continue;
					table.Add(fields[0], string.Join('\t', fields.Skip(1).Select(s => s.Trim())));
				}
				stream.ReadLine();
				stream.Close();
			}
			else
			{
				Console.WriteLine("Creating address override table");
				// UTF8 BOM for Excel
				StreamWriter stream = new StreamWriter(executablePath + "/../override.csv", false, Encoding.UTF8);
				stream.WriteLine(Resources.CoordinatesOverride.SeeReadme);
				stream.WriteLine(Resources.CoordinatesOverride.Headings);
				stream.WriteLine(Resources.CoordinatesOverride.XmlUrlOption(xmlUrl));
				stream.Close();
			}
			return (xmlUrl, await Amender.RequestOverrideLonglat(table));
		}

		private static async IAsyncEnumerator<UnitInformationEntry> ParseAndRequestUnit(XDocument root,
				XNamespace xmlnsUrl, CoordinatesOverrideStream overrides, string executablePath)
		{
			using StreamWriter overrideStream = new StreamWriter(executablePath + "/../override.csv", true, Encoding.UTF8);
			IEnumerator<UnitInformationEntry> GetEntriesFromXml()
			{
				foreach (XElement unit in root.Descendants(xmlnsUrl + "serviceUnit"))
				{
					Dictionary<string, string?> propDict = new();
					foreach (XElement prop in unit.Descendants())
						propDict.Add(prop.Name.LocalName, prop.Value);
					UnitInformationEntry.SharedAttributeKeys = propDict.Keys.ToArray();
					propDict.Add("addressOverride", null);
					yield return new UnitInformationEntry(propDict, UnitInformationEntry.MissingCoordinates);
				}
			}
			NetworkUtility.AddressLocator locator = address => AlsLocationInfo.FromAddress(
					address, true, client);
			IAsyncEnumerator<UnitInformationEntry> locatedEntries = GetEntriesFromXml().ToAsyncEnumerator()
					.ChunkComplete(entry => entry.Locate(locator), batchSize);
			while (await locatedEntries.MoveNextAsync())
			{
				string name = locatedEntries.Current["nameTChinese"].ToUpperInvariant();
				if (locatedEntries.Current.Coordinates == UnitInformationEntry.MissingCoordinates)
				{
					string address = locatedEntries.Current["addressTChinese"];
					if (!overrides.PendingChanges.ContainsKey(name))
						overrideStream.WriteLine(new CoordinatesOverrideEntry(name, address));
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
