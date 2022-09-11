#nullable enable

using System;
using System.Resources;

namespace WylieYYYY.GetinfoCSharp
{
	public static class Resources
	{
		// TODO: assembly name change pending
		private static ResourceManager _resourceManager = new("getinfo_csharp.GetinfoCSharp.Resources.Resources",
				typeof(Resources).Assembly);

		public static class Messages
		{
			public static string GenerateLocation(string unitInformationPath) =>
					string.Format(_resourceManager.GetString("Message_GenerateLocation")!, unitInformationPath);
			public static string InputCommand(string coordinatesOverridePath, string unitInformationPath) =>
					string.Format(_resourceManager.GetString("Message_InputCommand")!, coordinatesOverridePath, unitInformationPath);
			public static string FailedToLocate(string unitName) =>
					string.Format(_resourceManager.GetString("Message_FailedToLocate")!, unitName);
			public static string Located(string unitName) =>
					string.Format(_resourceManager.GetString("Message_Located")!, unitName);
			public static string TimeEstimation(TimeSpan estimatedTimeLeft) =>
					string.Format(_resourceManager.GetString("Message_TimeEstimation")!, estimatedTimeLeft);
			public static string ExitSuccess =>
					_resourceManager.GetString("Message_ExitSuccess")!;
			public static string ExitFailed =>
					_resourceManager.GetString("Message_ExitFailed")!;
		}

		public static class CoordinatesOverride
		{
			public static string SeeReadme =>
					_resourceManager.GetString("CoordinatesOverride_SeeReadme")!;
			public static string Headings =>
					_resourceManager.GetString("CoordinatesOverride_Headings")!;
			public static string XmlUrlOption(string url) =>
					string.Format(_resourceManager.GetString("CoordinatesOverride_XmlUrlOption")!, url);
		}

		public static class Exception
		{
			public static string LineTooLong =>
					_resourceManager.GetString("Exception_LineTooLong")!;
			public static string NotPositiveInteger(string paramName) =>
					string.Format(_resourceManager.GetString("Exception_NotPositiveInteger")!, paramName);
		}
	}
}
