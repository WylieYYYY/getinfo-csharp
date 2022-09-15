#nullable enable

using System;
using System.IO;

namespace WylieYYYY.GetinfoCSharp.IO
{
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
