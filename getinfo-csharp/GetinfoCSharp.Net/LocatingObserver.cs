#nullable enable

using System;
using System.Diagnostics;

using WylieYYYY.GetinfoCSharp.IO;

namespace WylieYYYY.GetinfoCSharp.Net
{
	// TODO: doc, and isolate console IO
	/// <summary>Observer that reports time estimation and status when locating entries.</summary>
	/// <remarks>This observer does not report when the locating is completed.</remarks>
	public class LocatingObserver : IObserver<LocatableEntry>
	{
		private Stopwatch stopwatch = new();
		private float completionRatio = 0, averageStepSize = 0;

		/// <summary>Initializes a locating observer.</summary>
		public LocatingObserver() => stopwatch.Start();

		/// <summary>
		///  Overridable method for reporting the completion ratio when an entry is located.
		///  Default is returning -1 to denote that an estimation is unavailable.
		/// </summary>
		/// <param name="entry">Located entry.</param>
		/// <param name="estimatedTimeLeft">
		///  Estimated time required for all entries to be located.
		/// </param>
		/// <returns>Ratio of completion ranging from 0 to 1, -1 for unavailable.</return>
		protected virtual float OnNext(LocatableEntry entry, TimeSpan estimatedTimeLeft) => -1;
		protected virtual float OnError(LocatableEntry.UnlocatableException ex,
				TimeSpan estimatedTimeLeft) => throw ex;
		protected virtual void OnCompleted(TimeSpan elapsedTime) {}

		public void OnNext(LocatableEntry entry)
		{
			float EstimationCallback(TimeSpan estimatedTimeLeft)
			{
				Console.WriteLine(Resources.Messages.Located(entry.Identifier!.ToString()!));
				PrintEstimation(estimatedTimeLeft);
				return OnNext(entry, estimatedTimeLeft);
			}
			EstimateTimeLeft(EstimationCallback);
		}
		public void OnError(Exception ex)
		{
			float EstimationCallback(TimeSpan estimatedTimeLeft)
			{
				LocatableEntry.UnlocatableException castedEx = (LocatableEntry.UnlocatableException)ex;
				Console.WriteLine(Resources.Messages.FailedToLocate(
						castedEx.Entry.Identifier!.ToString()!));
				PrintEstimation(estimatedTimeLeft);
				return OnError(castedEx, estimatedTimeLeft);
			}
			EstimateTimeLeft(EstimationCallback);
		}
		public void OnCompleted() => OnCompleted(stopwatch.Elapsed);

		private void EstimateTimeLeft(Func<TimeSpan, float> callback)
		{
			float predictedNextRatio = Math.Min(completionRatio + averageStepSize, 1);
			TimeSpan elapsedTime = stopwatch.Elapsed;
			TimeSpan estimatedTimeLeft = completionRatio == 0 ? TimeSpan.MaxValue :
					elapsedTime / completionRatio * predictedNextRatio - elapsedTime;
			float newCompletionRatio = callback(estimatedTimeLeft);
			if (newCompletionRatio == -1) return;
			averageStepSize = averageStepSize == 0 ? newCompletionRatio :
					newCompletionRatio / (completionRatio / averageStepSize + 1);
			completionRatio = newCompletionRatio;
		}

		private void PrintEstimation(TimeSpan estimatedTimeLeft)
		{
			if (estimatedTimeLeft == TimeSpan.MaxValue)
				Console.WriteLine(Resources.Messages.NoTimeEstimation);
			else Console.WriteLine(Resources.Messages.TimeEstimation(estimatedTimeLeft));
		}
	}
}
