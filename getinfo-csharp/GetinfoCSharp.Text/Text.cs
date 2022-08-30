#nullable enable

using System.Text.RegularExpressions;

namespace WylieYYYY.GetinfoCSharp.Text
{
	/// <summary>Utilities for convenient manipulation of strings.</summary>
	public static class TextUtility
	{
		/// <summary>Removes substrings that match any of the regular expressions.</summary>
		/// <param name="value">Reference of the string to remove substrings from.</summary>
		/// <param name="regexes">Regular expressions to be matched against.</param>
		public static void RegexRemove(ref string value, params string[] regexes)
		{ foreach (string regex in regexes) value = Regex.Replace(value, regex, string.Empty); }
	}
}
