using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Linq;

namespace getinfo_csharp
{
	struct GeoInfo
	{
		// may be int for index, string for address key in amending
		public string requestKey { get; private set; }
		public string address { get; private set; }
		public XDocument root { get; private set; }
		public Tuple<float, float> longlat { get; private set; }

		private static readonly HttpClient client = new HttpClient();
		private static readonly Random random = new Random();

		// process longitude and latitude from XML response
		async public static Task<GeoInfo> FromUrl(string requestUrl, Pacer estimatePacer)
		{
			string address = HttpUtility.UrlDecode(requestUrl.Split("&q=")[1].Split("&i=")[0]);
			string response;
			try { response = await client.GetStringAsync(requestUrl); }
			// recovery prompt
			catch (HttpRequestException)
			{
				Console.WriteLine("Request failed for " + address + ", trying again");
				try { response = await client.GetStringAsync(requestUrl); }
				catch (HttpRequestException)
				{
					Console.WriteLine("Giving up, try diagnose network and rerun later");
					Console.ReadLine();
					// add exception to indicate end of branch
					throw new HttpRequestException();
				}
			}
			Console.WriteLine("Got response for " + address);
			Console.WriteLine("[Estimated time left: " + estimatePacer.Step().ToString() + ']');
			// traverse into the tree
			XDocument root = XDocument.Parse(response);
			float lon = float.Parse(root.Descendants("Longitude").First().Value);
			float lat = float.Parse(root.Descendants("Latitude").First().Value);
			// distort slightly to reduce the chance of overlap
			lon += random.Next(-50, 50) / 1000000;
			lat += random.Next(-50, 50) / 1000000;
			return new GeoInfo(requestUrl.Split("&i=")[1], address, root, lon, lat);
		}

		private GeoInfo(string requestKey, string address, XDocument root, float lon, float lat)
		{
			this.requestKey = requestKey;
			this.address = address;
			this.root = root;
			this.longlat = new Tuple<float, float>(lon, lat);
		}
	}
}