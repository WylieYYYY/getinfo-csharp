#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
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
		// FIXME: exception handling for this method
		public string? this[string key]
		{
			get
			{
				try { return _attributes[key]; }
				catch (KeyNotFoundException ex)
				{ throw new KeyNotFoundException(Resources.Exception.EntryAttributeKeyNotFound(key), ex); }
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
			if (other == null) throw new ArgumentNullException(nameof(other));
			int order = other.Coordinates.Y.CompareTo(Coordinates.Y);
			return order == 0 ? 1 : order;
		}
	}

	/// <summary>Stream for reading and writing unit information file.</summary>
	public class UnitInformationStream
	{
		private StreamBuilder _builder;

		/// <summary>Initializes a unit information stream.</summary>
		/// <param name="builder">Builder for the backing stream, must also be seekable.</param>
		public UnitInformationStream(StreamBuilder builder) => _builder = builder;

		/// <summary>Write entries to the backing stream constructed by the builder.</summary>
		/// <param name="entries">Entries to be written.</param>
		/// <exception cref="IOException"/>
		public async Task WriteEntries(IAsyncEnumerator<UnitInformationEntry> entries)
		{
			SortedSet<UnitInformationEntry> sortedEntries = new();
			while (await entries.MoveNextAsync()) sortedEntries.Add(entries.Current);
			using StreamWriter writer = new(_builder(FileMode.OpenOrCreate), new UTF8Encoding(false));
			await writer.WriteAsync(Resources.UnitInformation.VariableDefinitionPreamble("longlat"));
			await writer.WriteLineAsync(JsonSerializer.Serialize(sortedEntries.Select(
					entry => new float[] { entry.Coordinates.X, entry.Coordinates.Y })) + ';');
			await writer.WriteAsync(Resources.UnitInformation.VariableDefinitionPreamble("unitinfo"));
			string[] attributeKeys = UnitInformationEntry.SharedAttributeKeys!;
			IEnumerable<IEnumerable<string?>> compiledAttributes = sortedEntries
					.Select(entry => attributeKeys.Select(key => entry[key]));
			await writer.WriteAsync(JsonSerializer.Serialize(compiledAttributes.Prepend(attributeKeys)));
			await writer.WriteLineAsync(';');
			// TODO: verify that the stream is seekable
			writer.BaseStream.SetLength(writer.BaseStream.Position);
		}
	}
}
