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
	public class CoordinatesOverrideEntry : LocatableEntry
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
		/// <summary>Locate status of the entry, checks <see cref="OverridingCoordinates"/>.</summary>
		public bool Located => OverridingCoordinates != null;

		private const string NoOverridePlaceholder = "[NO OVERRIDE]";

		/// <summary>Initializes an coordinates override entry.</summary>
		/// <param name="traditionalChineseName"><see cref="TraditionalChineseName"/></param>
		/// <param name="registeredAddress"><see cref="RegisteredAddress"/></param>
		/// <param name="proposedAddress"><see cref="ProposedAddress"/></param>
		/// <param name="coordinatesOffset"><see cref="CoordinatesOffset"/></param>
		public CoordinatesOverrideEntry(string traditionalChineseName, string registeredAddress,
				string? proposedAddress = null, Vector2 coordinatesOffset = default)
		{
			this.TraditionalChineseName = traditionalChineseName.ToUpperInvariant().Replace("\t", "");
			this.RegisteredAddress = registeredAddress.Replace("\t", "");
			this.ProposedAddress = proposedAddress == NoOverridePlaceholder ?
					null : proposedAddress?.Replace("\t", "");
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
						((LocatableEntry)this).Identifier!.ToString()!));
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
			if (OverridingCoordinates == null)
				throw new InvalidOperationException(Resources.Exception.OverrideBeforeLocate);
			entry.Coordinates = (Vector2)OverridingCoordinates;
		}

		/// <summary>Serializes the entry into a tab separated form.</summary>
		/// <returns>Serialized form of the entry.</returns>
		public override string ToString() => string.Join('\t',
				TraditionalChineseName, ProposedAddress ?? NoOverridePlaceholder,
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
		private SemaphoreSlim _readSemaphore = new(1, 1);
		private string? _overreadLine = null;
		private int _batchSize = 50;
		private Dictionary<object, CoordinatesOverrideEntry> _pendingChanges = new();
		private StreamWriter? _writer = null;
		private SemaphoreSlim _writeSemaphore = new(1, 1);

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
		/// <param name="locatableObserver">Observer for the reading and locating of entries.</param>
		/// <exception cref="FileFormatException"/>
		public async Task ReadLocatedEntries(NetworkUtility.AddressLocator locator,
				IObserver<LocatableEntry> locatableObserver)
		{
			IAsyncEnumerator<CoordinatesOverrideEntry> entries = ReadEntries()
					.ChunkComplete(entry => entry.Locate(locator), BatchSize);
			while (await entries.MoveNextAsync())
			{
				// retain the old replacing behaviour, may change in a later version
				if (!_pendingChanges.TryAdd(((LocatableEntry)entries.Current).Identifier!,
						entries.Current))
					_pendingChanges[((LocatableEntry)entries.Current).Identifier!] = entries.Current;
				// TODO: special next status for non-overriding entries
				if (entries.Current.ProposedAddress == null ||
						entries.Current.OverridingCoordinates != null)
					locatableObserver.OnNext(entries.Current);
				else locatableObserver.OnError(new LocatableEntry.UnlocatableException(entries.Current));
			}
			locatableObserver.OnCompleted();
		}

		/// <summary>Overrides entries with matching modification identifier.</summary>
		/// <param name="entries">Asynchronous enumerator of entries to be overriden.</param>
		/// <returns>Asynchronous enumerator of overriden entries.</returns>
		public async IAsyncEnumerator<UnitInformationEntry> OverrideEntries(
				IAsyncEnumerator<UnitInformationEntry> entries)
		{
			while (await entries.MoveNextAsync())
			{
				object? modificationIdentifier = ((LocatableEntry)entries.Current).Identifier;
				if (modificationIdentifier != null && _pendingChanges.ContainsKey(modificationIdentifier))
					_pendingChanges[modificationIdentifier].OverrideEntry(entries.Current);
				yield return entries.Current;
			}
		}

		/// <summary>
		///  Write a <see cref="CoordinatesOverrideEntry"/> to the backing stream,
		///  adding a preamble if the backing stream represents a new backing file.
		/// </summary>
		/// <param name="entry">Entry to be written to the backing stream.</param>
		/// <exception cref="InvalidOperationException">
		///  If the backing file is not fully read,
		///  all entries and configuration must be read before writing.
		/// </exception>
		public async Task WriteEntry(CoordinatesOverrideEntry entry)
		{
			// reading is prohibited after writing has commence
			if (_readSemaphore.CurrentCount != 0) await _readSemaphore.WaitAsync();
			using SemaphoreHandle semaphoreHandle = await _writeSemaphore.WaitHandle();
			if (_stream.ReadByte() != -1)
				throw new InvalidOperationException(Resources.Exception.WriteBeforeReadAll);
			bool hasNoPreviousWrite = _writer == null;
			_writer ??= new StreamWriter(_stream, new UTF8Encoding(true), leaveOpen: true);
			// TODO: verify that Position is available
			if (_stream.Position == 0)
			{
				await _writer.WriteLineAsync(Resources.CoordinatesOverride.SeeReadme);
				if (SourceUrl != null)
					await _writer.WriteLineAsync(Resources.CoordinatesOverride.XmlUrlOption(SourceUrl));
				await _writer.WriteLineAsync(Resources.CoordinatesOverride.BatchSizeOption(BatchSize));
				await _writer.WriteLineAsync(Resources.CoordinatesOverride.Headings);
				await _writer.FlushAsync();
			}
			else if (hasNoPreviousWrite) await _writer.WriteLineAsync();
			if (!_pendingChanges.ContainsKey(((LocatableEntry)entry).Identifier!))
				await _writer.WriteLineAsync(entry.ToString());
		}

		/// <summary>Disposes the underlying stream, reader, writer, and semaphore.</summary>
		public void Dispose()
		{
			_reader.Dispose();
			_writer?.Dispose();
			_stream.Dispose();
			_readSemaphore.Dispose();
			_writeSemaphore.Dispose();
		}

		/// <summary>Gets the modification identifier for the given entry.</summary>
		/// <remarks>
		///  Identifier type should be kept opaque, only value equality should be assumed.
		///  Will always be non-null if <paramref name="entry"/> is a
		///  <see cref="CoordinatesOverrideEntry"/>.
		/// </remarks>
		/// <param name="entry">Entry to get removal identifier for.</param>
		/// <returns>
		///  Identifier to tag entry for modification, null if no modification should take place.
		/// </returns>
		/// <exception cref="ArgumentException">If the type of the entry is not allowed.</exception>
		internal static object? GetModificationIdentifier(object? entry) => entry switch
		{
			UnitInformationEntry castedEntry => castedEntry["nameTChinese"]?.ToUpperInvariant()
					.Replace("\t", ""),
			CoordinatesOverrideEntry castedEntry => castedEntry.TraditionalChineseName,
			_ => throw new ArgumentException(Resources.Exception.EntryTypeNotSupported(
					entry?.GetType()), nameof(entry)),
		};

		/// <exception cref="FileFormatException"/>
		private async IAsyncEnumerator<string[]> ReadValidLines(bool forEntries, int minimumFieldCount)
		{
			using SemaphoreHandle semaphoreHandle = await _readSemaphore.WaitHandle();
			try { if ((_overreadLine ??= await _reader.ReadLineAsync()) == null) yield break; }
			catch (ArgumentOutOfRangeException ex)
			{ throw new FileFormatException(Resources.Exception.LineTooLong, ex); }
			Func<string, bool> IsConfigurationOrComment = s => s.Any(c => char.IsLower(c));
			Func<string, bool> IsEntryOrComment = s => s.Any(c => !char.IsLower(c) && c != '_');
			string[]? fields = null;
			Func<string, bool> IsValid = forEntries ? IsEntryOrComment : IsConfigurationOrComment;
			Func<string, bool> IsComment = forEntries ? IsConfigurationOrComment : IsEntryOrComment;
			// lines starting with tab character are not valid
			while (IsValid((fields = _overreadLine!.Split('\t'))[0]) || _overreadLine == string.Empty)
			{
				if (!IsComment(fields[0]) && _overreadLine != string.Empty)
				{
					if (fields.Length < minimumFieldCount)
					{
						throw new FileFormatException(Resources.Exception.IncorrectFieldCount(
								minimumFieldCount, fields.Length));
					}
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
				if (!float.TryParse(fields[2], out coordinates.X))
					throw new FileFormatException(Resources.Exception.CoordinatesValueNotFloat(fields[2]));
				if (!float.TryParse(fields[3], out coordinates.Y))
					throw new FileFormatException(Resources.Exception.CoordinatesValueNotFloat(fields[3]));
				yield return new CoordinatesOverrideEntry(fields[0], fields[4], fields[1], coordinates);
			}
		}
	}
}
