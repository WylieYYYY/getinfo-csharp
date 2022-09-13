#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

using WylieYYYY.GetinfoCSharp.Net;

namespace WylieYYYY.GetinfoCSharp.IO
{
	/// <summary>Represents an entry within the unit information file.</summary>
	public class UnitInformationEntry : IComparable<UnitInformationEntry>
	{
		// TODO: remove shared attribute keys after storage class integration
		[System.Obsolete("Temporary keys container before storage class integration.")]
		public static string[]? SharedAttributeKeys = null;

		/// <summary>Coordinates to use when the entry currently does not have coordinates.</summary>
		public static Vector2 MissingCoordinates { get => new Vector2(0, -91); }
		/// <summary>Coordinates of the unit.</summary>
		public Vector2 Coordinates;

		private readonly Dictionary<string, string?> _attributes;

		/// <summary>Initializes a unit information entry.</summary>
		/// <param name="attributes">Attributes that contain information of the unit.</param>
		/// <param name="coordinates"><see cref="Coordinates"/></param>
		public UnitInformationEntry(Dictionary<string, string?> attributes, Vector2 coordinates)
		{
			_attributes = attributes;
			Coordinates = coordinates;
		}

		/// <summary>Locates this entry using the English address attribute.</summary>
		/// <param name="locator">Locator for the English address.</param>
		/// <returns>This entry asynchronously for convenient continuation.</returns>
		/// <exception cref="FileFormatException">If the address attribute is missing.</exception>
		public async Task<UnitInformationEntry> Locate(NetworkUtility.AddressLocator locator)
		{
			string? address = this["addressEnglish"];
			if (address == null) throw new FileFormatException("" /*HACK*/);
			AlsLocationInfo? locationInfo = await locator(address);
			string? normalizedDistrict = this["districtEnglish"]?.Replace(" AND ", " & ")
					.ToUpperInvariant();
			string? fetchedDistrict = locationInfo?.District;
			// skip district test if either sources does not have district information
			bool shouldSkipDistrictCheck = normalizedDistrict == null || fetchedDistrict == null;
			if (locationInfo == null) return this;
			if (shouldSkipDistrictCheck || fetchedDistrict!.Contains(normalizedDistrict!))
				Coordinates = locationInfo.Coordinates;
			return this;
		}

		/// <summary>Gets the value of the attribute denoted by <paramref name="key"/>.</summary>
		/// <param name="key">Key of the attribute.</param>
		/// <returns>Value of the attribute.</returns>
		/// <exception cref="KeyNotFoundException"/>
		public string? this[string key]
		{
			get
			{
				try { return _attributes[key]; }
				catch (KeyNotFoundException ex) { throw new KeyNotFoundException("" /*HACK*/, ex); }
			}
			set
			{
				// ensure that key exists and throw appropriate exception if not
				_ = this[key];
				_attributes[key] = value;
			}
		}

		/// <summary>Defines sorting by decreasing latitude.</summary>
		/// <remarks>
		///  This method will never return zero, for use in <see cref="SortedSet{T}"/>.
		/// </remarks>
		/// <param name="other">Other entry to be compared against.</param>
		/// <returns>Relative order of the objects being compared.</returns>
		/// <exception cref="ArgumentNullException"/>
		public int CompareTo(UnitInformationEntry? other)
		{
			if (other == null) throw new ArgumentNullException(/*HACK*/);
			int order = other.Coordinates.Y.CompareTo(Coordinates.Y);
			return order == 0 ? 1 : order;
		}
	}
}
