#nullable enable

using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;

namespace WylieYYYY.GetinfoCSharp.Text.Xml
{
	/// <summary>Utility for convenient XML document traversal.</summary>
	public static class XmlUtility
	{
		/// <summary>
		///  Gets the only descendant of <paramref name="container"/> that is selected by
		///  <paramref name="name"/>.
		/// </summary>
		/// <param name="container">Container of the target descendant element.</param>
		/// <param name="name">Name that selects the appropriate descendant.</param>
		/// <returns>The selected element.</returns>
		/// <exception cref="XmlException">
		///  If there are none or more than one descendants selected by <paramref name="name"/>.
		/// </exception>
		public static XElement OnlyDescendant(this XContainer container, XName name)
		{
			IEnumerator<XElement> descendantEnumerator = container.Descendants(name).GetEnumerator();
			if (!descendantEnumerator.MoveNext()) throw new XmlException(/*HACK*/);
			XElement targetElement = descendantEnumerator.Current;
			if (descendantEnumerator.MoveNext()) throw new XmlException(/*HACK*/);
			return targetElement;
		}
	}
}
