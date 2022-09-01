#nullable enable

using System;

namespace WylieYYYY.GetinfoCSharp.IO
{
	/// <summary>Exception thrown when the file format is non-conforming.</summary>
	public class FileFormatException : Exception
	{
		public FileFormatException() {}
		public FileFormatException(string message) : base(message) {}
		public FileFormatException(string message, Exception inner) : base(message, inner) {}
	}
}
