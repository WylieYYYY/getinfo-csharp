#nullable enable

using System;
using System.Collections.Generic;
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

		/// <summary>Overrides the given entry with the available properties.</summary>
		/// <param name="entry">Entry to be overriden.</param>
		/// <exception cref="KeyNotFoundException">If an overriden property is missing.</exception>
		public void OverrideEntry(UnitInformationEntry entry)
		{
			entry["addressOverride"] = ProposedAddress;
			// TODO: null check for overriding coordinates
			entry.Coordinates = (Vector2)OverridingCoordinates!;
		}

		/// <summary>Serializes the entry into a tab separated form.</summary>
		/// <returns>Serialized form of the entry.</returns>
		public override string ToString() => string.Join('\t',
				TraditionalChineseName.ToUpperInvariant(), ProposedAddress ?? NoOverridePlaceholder,
				CoordinatesOffset.X, CoordinatesOffset.Y, RegisteredAddress);
	}

	/// <summary>Stream for reading and writing coordinates override file in one pass.</summary>
	public class CoordinatesOverrideStream
	{
		// TODO: make this private when read method is implemented
		[Obsolete("Make this private after reading from file is implemented.")]
		public Dictionary<object, CoordinatesOverrideEntry> PendingChanges = new();

		/// <summary>Overrides entries with matching modification identifier.</summary>
		/// <param name="entries">Asynchronous enumerator of entries to be overriden.</param>
		/// <returns>Asynchronous enumerator of overriden entries.</returns>
		public async IAsyncEnumerator<UnitInformationEntry> OverrideEntries(
				IAsyncEnumerator<UnitInformationEntry> entries)
		{
			while (await entries.MoveNextAsync())
			{
				object? modificationIdentifier = GetModificationIdentifier(entries.Current);
				if (modificationIdentifier != null && PendingChanges.ContainsKey(modificationIdentifier))
					PendingChanges[modificationIdentifier].OverrideEntry(entries.Current);
				yield return entries.Current;
			}
		}

		/// <summary>Gets the modification identifier for the given entry.</summary>
		/// <remarks>
		///  Identifier type should be kept opaque, only value equality should be assumed.
		/// </remarks>
		/// <param name="entry">Entry to get removal identifier for.</param>
		/// <returns>
		///  Identifier to tag entry for modification,
		///  null if no modification should take place.
		/// </returns>
		/// <exception cref="ArgumentException">If the type of the entry is not allowed.</exception>
		private static object? GetModificationIdentifier(object? entry) => entry switch
		{
			UnitInformationEntry castedEntry => castedEntry["nameTChinese"]?.ToUpperInvariant(),
			CoordinatesOverrideEntry castedEntry => castedEntry.TraditionalChineseName,
			_ => throw new ArgumentException(nameof(entry)/*HACK*/),
		};
	}
}
