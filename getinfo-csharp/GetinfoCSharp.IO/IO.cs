#nullable enable

using System;
using System.IO;

namespace WylieYYYY.GetinfoCSharp.IO
{
	/// <summary>Entries that are locatable.</summary>
	public interface LocatableEntry
	{
		/// <summary>Whether this entry has been located.</summary>
		public bool Located { get; }
		/// <summary>Readable modification identifier for this entry.</summary>
		public object? Identifier =>
				CoordinatesOverrideStream.GetModificationIdentifier(this);

		/// <summary>Exception for reporting to observer that an entry cannot be located.</summary>
		public class UnlocatableException : Exception
		{
			/// <summary>The unlocatable entry.</summary>
			public readonly LocatableEntry Entry;

			// TODO: change to get message here
			/// <summary>Initializes an reporting exception for an unlocatable entry.</summary>
			internal UnlocatableException(LocatableEntry entry) => Entry = entry;
		}
	}

	/// <summary>Builder to construct multiple streams to the same file.</summary>
	/// <param name="mode">See <see cref="FileMode"/>.</param>
	/// <returns>Read and writable stream pointing to the file opened with specified mode.</returns>
	public delegate Stream StreamBuilder(FileMode mode);
	/// <summary>Exception thrown when the file format is non-conforming.</summary>
	public class FileFormatException : Exception
	{
		public FileFormatException() {}
		public FileFormatException(string message) : base(message) {}
		public FileFormatException(string message, Exception inner) : base(message, inner) {}
	}
}
