#nullable enable

using System.Numerics;
using System.Threading.Tasks;

using WylieYYYY.GetinfoCSharp.Net;

namespace WylieYYYY.GetinfoCSharp.IO
{
	/// <summary>Represents an entry within the coordinates override file.</summary>
	public class CoordinatesOverrideEntry
	{
		/// <summary>Traditional Chinese name as identifier.</summary>
		public readonly string TraditionalChineseName;
		/// <summary>Reference location for coordinates approximation.</summary>
		public readonly string? ProposedAddress;
		/// <summary>Coordinates offset from the reference location.</summary>
		public readonly Vector2 CoordinatesOffset;
		/// <summary>Unit's registered address that is unresolved.</summary>
		public readonly string RegisteredAddress;
		/// <summary>Calculated coordinates for overriding, null if not yet located.</summary>
		public Vector2? OverridingCoordinates { get; private set; } = null;

		private const string NoOverridePlaceholder = "[NO OVERRIDE]";

		/// <summary>Initializes an coordinates override entry.</summary>
		/// <param name="traditionalChineseName"><see cref="TraditionalChineseName"/></param>
		/// <param name="registeredAddress"><see cref="RegisteredAddress"/></param>
		/// <param name="proposedAddress"><see cref="ProposedAddress"/></param>
		/// <param name="coordinatesOffset"><see cref="CoordinatesOffset"/></param>
		public CoordinatesOverrideEntry(string traditionalChineseName, string registeredAddress,
				string? proposedAddress = null, Vector2 coordinatesOffset = default)
		{
			this.TraditionalChineseName = traditionalChineseName.ToUpperInvariant();
			this.RegisteredAddress = registeredAddress;
			this.ProposedAddress = proposedAddress == NoOverridePlaceholder ? null : proposedAddress;
			this.CoordinatesOffset = coordinatesOffset;
		}

		/// <summary>Locates and calculates <see cref="OverridingCoordinates"/>.</summary>
		/// <param name="locator">Locator for <see cref="ProposedAddress"/>.</param>
		/// <returns>This entry asynchronously for convenient continuation.</returns>
		/// <exception cref="FileFormatException">If the ALS cannot locate the address.</exception>
		public async Task<CoordinatesOverrideEntry> Locate(NetworkUtility.AddressLocator locator)
		{
			if (ProposedAddress == null) return this;
			AlsLocationInfo? locationInfo = await locator(ProposedAddress);
			if (locationInfo == null) throw new FileFormatException(/*HACK*/);
			OverridingCoordinates = locationInfo.Coordinates + CoordinatesOffset;
			return this;
		}

		/// <summary>Serializes the entry into a tab separated form.</summary>
		/// <returns>Serialized form of the entry.</returns>
		public override string ToString() => string.Join('\t',
				TraditionalChineseName.ToUpperInvariant(), ProposedAddress ?? NoOverridePlaceholder,
				CoordinatesOffset.X, CoordinatesOffset.Y, RegisteredAddress);
	}
}
