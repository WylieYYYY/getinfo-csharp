using System;
using System.Diagnostics;

namespace getinfo_csharp
{
	class Pacer
	{
		int totalCount, currentIndex = 0;
		private readonly Stopwatch stopwatch = new Stopwatch();
		public Pacer(int totalCount)
		{
			this.totalCount = totalCount;
			stopwatch.Start();
		}
		public TimeSpan Step()
		{
			if (currentIndex == totalCount) throw new Exception("Pacer stepped out of bound.");
			return ((float)totalCount / (++currentIndex) - 1) * stopwatch.Elapsed;
		}
		public void Stop() { stopwatch.Stop(); }
	}
}
