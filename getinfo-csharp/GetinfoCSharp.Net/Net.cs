#nullable enable

using System;
using System.Net.Http;
using System.Numerics;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;

using WylieYYYY.GetinfoCSharp.Text.Xml;

namespace WylieYYYY.GetinfoCSharp.Net
{
	/// <summary>Utilities for sending network requests efficiently.</summary>
	public static class NetworkUtility
	{
		/// <summary>
		///  Locator that accepts a normalized address and returns location information.
		/// </summary>
		/// <param name="address">Non-normalized address for ALS query.</param>
		/// <returns>
		///  An <see cref="AlsLocationInfo"/> if the address has been located successfully,
		///  or null if the address cannot be located.
		/// </returns>
		public delegate Task<AlsLocationInfo?> AddressLocator(string address);
		/// <summary>Requests for a response with the given URL, retries if failed.</summary>
		/// <param name="client">Client for sending requests.</param>
		/// <param name="url">URL to send GET requests to.</param>
		/// <returns>Asynchronous string of the response body.</returns>
		/// <exception cref="HttpRequestException">When both requests failed.</exception>
		public static async Task<string> RequestTwiceOrBail(this HttpClient client, string url)
		{
			return await Utility.AttemptRetry<string, HttpRequestException>(
					() => client.GetStringAsync(url), 2, () => Task.Delay(TimeSpan.FromMilliseconds(100)));
		}
	}

	/// <summary>Queried location information from an ALS.</summary>
	public partial record AlsLocationInfo
	{
		/// <summary>Source address of the location information.</summary>
		public readonly string SourceAddress;
		/// <summary>Resolved longitude and latitude of the address.</summary>
		public readonly Vector2 Coordinates;
		/// <summary>
		///  Resolved district in English for reliability evaluation, null if unavailable.
		/// </summary>
		public readonly string? District;

		private static Random _random = new();
		private const string AlsProviderPrefix = "https://www.als.ogcio.gov.hk/lookup?n=1&q=";

		/// <summary>Queries location information by address.</summary>
		/// <param name="address">Address to be queried for location information.</param>
		/// <param name="shouldNormalize">
		///  Whether to normalize the address using ALS specific algorithm,
		///  or to accept <paramref name="address"/> verbatim.
		/// </param>
		/// <param name="client">Client for sending requests.</param>
		/// <returns>Asynchronous location information.</returns>
		/// <exception cref="HttpRequestException"/>
		/// <exception cref="XmlException">
		///  If the response from the ALS is not a valid XML document,
		///  or if the XML is not in the expected format.
		/// </exception>
		public static async Task<AlsLocationInfo?> FromAddress(string address, bool shouldNormalize,
				HttpClient httpClient)
		{
			string? normalizedAddress = shouldNormalize ? NormalizeAddress(address) : address;
			if (normalizedAddress == null) return null;
			string response = await httpClient.RequestTwiceOrBail(AlsProviderPrefix +
					HttpUtility.UrlEncode(normalizedAddress));
			XDocument responseXmlDocument = XDocument.Parse(response);
			XContainer? locatedEntry = null;
			try { locatedEntry = responseXmlDocument.OnlyDescendant("SuggestedAddress"); }
			catch (XmlException) { return null; }
			// offset by a small amount to prevent overlapping
			Func<string, float> getCoordinate = name => {
				string namedElementValue = locatedEntry.OnlyDescendant(name).Value;
				if (!float.TryParse(namedElementValue, out float value))
					throw new XmlException(Resources.Exception.CoordinatesValueNotFloat(namedElementValue));
				return value + _random.Next(-50, 50) / 1000000;
			};
			Vector2 coordinates = new(getCoordinate("Longitude"), getCoordinate("Latitude"));
			string? district = null;
			try
			{ district = locatedEntry.OnlyDescendant("EngDistrict").OnlyDescendant("DcDistrict").Value; }
			catch (XmlException) { /* district data unavailable, district remains null */ }
			return new AlsLocationInfo(address, coordinates, district);
		}

		/// <remarks>Use <see cref="FromAddress"/> instead.</remarks>
		private AlsLocationInfo(string sourceAddress, Vector2 coordinates, string? district)
		{
			SourceAddress = sourceAddress;
			Coordinates = coordinates;
			District = district;
		}
	}
}
