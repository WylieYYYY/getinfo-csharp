#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
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
			if (locationInfo == null)
			{
				throw new FileFormatException(Resources.Messages.FailedToLocate(
						CoordinatesOverrideStream.GetModificationIdentifier(this)!.ToString()!));
			}
			OverridingCoordinates = locationInfo.Coordinates + CoordinatesOffset;
			return this;
		}

		/// <summary>Overrides the given entry with the available properties.</summary>
		/// <param name="entry">Entry to be overriden.</param>
		/// <exception cref="InvalidOperationException">
		///  If this override entry is locatable, but is not located.
		/// </exception>
		/// <exception cref="KeyNotFoundException">If an overriden property is missing.</exception>
		public void OverrideEntry(UnitInformationEntry entry)
		{
			entry["addressOverride"] = ProposedAddress;
			if (ProposedAddress == null) return;
			if (OverridingCoordinates == null) throw new InvalidOperationException(/*HACK*/);
			entry.Coordinates = (Vector2)OverridingCoordinates;
		}

		/// <summary>Serializes the entry into a tab separated form.</summary>
		/// <returns>Serialized form of the entry.</returns>
		public override string ToString() => string.Join('\t',
				TraditionalChineseName.ToUpperInvariant(), ProposedAddress ?? NoOverridePlaceholder,
				CoordinatesOffset.X, CoordinatesOffset.Y, RegisteredAddress);
	}

	/// <summary>Stream for reading and writing coordinates override file in one pass.</summary>
	public class CoordinatesOverrideStream : IDisposable
	{
		/// <summary>Source URL where this coordinates override is valid.</summary>
		public string? SourceUrl { get; private set; }
		/// <summary>Determines how many ALS queries should be done in parallel.</summary>
		public int BatchSize => _batchSize;

		private Stream _stream;
		private StreamReader _reader;
		private SemaphoreSlim _semaphore = new(1, 1);
		private string? _overreadLine = null;
		private int _batchSize = 50;
		// TODO: make this private when read method is implemented
		[Obsolete("Make this private after reading from file is implemented.")]
		public Dictionary<object, CoordinatesOverrideEntry> PendingChanges = new();

		/// <summary>Initializes a coordinates override stream.</summary>
		/// <param name="stream">Stream to read or write entries, usually to the backing file.</param>
		public CoordinatesOverrideStream(Stream stream)
		{
			_stream = stream;
			_reader = new(_stream, new UTF8Encoding(true), leaveOpen: true);
		}

		/// <summary>Reads lines of comments and configurations, populating properties.</summary>
		/// <exception cref="FileFormatException"/>
		public async Task ReadConfigurations()
		{
			const int ConfigurationFieldCount = 2;
			IAsyncEnumerator<string[]> fieldsEnumerator =
					ReadValidLines(forEntries: false, ConfigurationFieldCount);
			while (await fieldsEnumerator.MoveNextAsync())
			{
				string[] fields = fieldsEnumerator.Current;
				if (fields[0] == "xml_url") SourceUrl = fields[1];
				if (fields[0] == "batch_size" &&
						(!int.TryParse(fields[1], out _batchSize) || _batchSize <= 0))
					throw new FileFormatException(Resources.Exception.NotPositiveInteger("batch_size"));
			}
		}

		/// <summary>Reads lines of comments and entries, adding for overrides.</summary>
		/// <param name="locator">
		///  Locator for <see cref="CoordinatesOverrideEntry.Locate(NetworkUtility.AddressLocator)"/>.
		/// </param>
		/// <returns>Asynchronous enumerator of located entries' stringable identifiers.</returns>
		/// <exception cref="FileFormatException">/*TODO: doc*/</exception>
		public async IAsyncEnumerator<object> ReadLocatedEntries(NetworkUtility.AddressLocator locator)
		{
			IAsyncEnumerator<CoordinatesOverrideEntry> entries = ReadEntries()
					.ChunkComplete(entry => entry.Locate(locator), BatchSize);
			while (await entries.MoveNextAsync())
			{
				object identifier = GetModificationIdentifier(entries.Current)!;
				if (!PendingChanges.TryAdd(identifier, entries.Current))
					throw new FileFormatException(/*HACK*/);
				if (entries.Current.ProposedAddress == null) continue;
				yield return identifier;
			}
		}

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

		/// <summary>Disposes the underlying stream, reader, and semaphore.</summary>
		public void Dispose()
		{
			_reader.Dispose();
			_stream.Dispose();
			_semaphore.Dispose();
		}

		/// <summary>Gets the modification identifier for the given entry.</summary>
		/// <remarks>
		///  Identifier type should be kept opaque, only value equality should be assumed.
		///  Will always be non-null if <paramref name="entry"/> is a
		///  <see cref="CoordinatesOverrideEntry"/>.
		/// </remarks>
		/// <param name="entry">Entry to get removal identifier for.</param>
		/// <returns>
		///  Identifier to tag entry for modification,
		///  null if no modification should take place.
		/// </returns>
		/// <exception cref="ArgumentException">If the type of the entry is not allowed.</exception>
		internal static object? GetModificationIdentifier(object? entry) => entry switch
		{
			UnitInformationEntry castedEntry => castedEntry["nameTChinese"]?.ToUpperInvariant(),
			CoordinatesOverrideEntry castedEntry => castedEntry.TraditionalChineseName,
			_ => throw new ArgumentException(nameof(entry)/*HACK*/),
		};

		/// <exception cref="FileFormatException"/>
		private async IAsyncEnumerator<string[]> ReadValidLines(bool forEntries, int minimumFieldCount)
		{
			using SemaphoreHandle semaphoreHandle = await _semaphore.WaitHandle();
			try { if ((_overreadLine ??= await _reader.ReadLineAsync()) == null) yield break; }
			catch (ArgumentOutOfRangeException ex)
			{ throw new FileFormatException(Resources.Exception.LineTooLong, ex); }
			Func<string, bool> IsConfigurationOrComment = s => s.Any(c => char.IsLower(c));
			Func<string, bool> IsEntryOrComment = s => s.Any(c => !char.IsLower(c) && c != '_');
			string[]? fields = null;
			Func<string, bool> IsValid = forEntries ? IsEntryOrComment : IsConfigurationOrComment;
			Func<string, bool> IsComment = forEntries ? IsConfigurationOrComment : IsEntryOrComment;
			// lines starting with tab character are not valid
			while (IsValid((fields = _overreadLine!.Split('\t'))[0]))
			{
				if (!IsComment(fields[0]))
				{
					if (fields.Length < minimumFieldCount)
						throw new FileFormatException(/*HACK*/);
					yield return fields;
				}
				try { if ((_overreadLine = await _reader.ReadLineAsync()) == null) yield break; }
				catch (ArgumentOutOfRangeException ex)
				{ throw new FileFormatException(Resources.Exception.LineTooLong, ex); }
			}
		}

		/// <exception cref="FileFormatException"/>
		private async IAsyncEnumerator<CoordinatesOverrideEntry> ReadEntries()
		{
			const int EntryFieldCount = 5;
			IAsyncEnumerator<string[]> fieldsEnumerator =
					ReadValidLines(forEntries: true, EntryFieldCount);
			while (await fieldsEnumerator.MoveNextAsync())
			{
				string[] fields = fieldsEnumerator.Current;
				Vector2 coordinates = new();
				if (!float.TryParse(fields[2], out coordinates.X) ||
						!float.TryParse(fields[3], out coordinates.Y))
					throw new FileFormatException(/*HACK*/);
				yield return new CoordinatesOverrideEntry(fields[0], fields[4], fields[1], coordinates);
			}
		}
	}
}
