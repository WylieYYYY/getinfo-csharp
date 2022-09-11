#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace WylieYYYY.GetinfoCSharp
{
	/// <inheritdoc/>
	public static partial class Utility
	{
		/// <summary>Waits for the semaphore and gets the handle representing the signal.</summary>
		/// <param name="semaphore">The <see cref="SemaphoreSlim"/> to wait for.</param>
		/// <returns>Asynchronous handle representing the signal to be released later.</returns>
		public static async Task<SemaphoreHandle> WaitHandle(this SemaphoreSlim semaphore)
		{
			await semaphore.WaitAsync();
			return new SemaphoreHandle(semaphore);
		}
	}

	/// <summary>Handle that releases the semaphore once disposed.</summary>
	public struct SemaphoreHandle : IDisposable
	{
		private readonly SemaphoreSlim _semaphore;

		/// <summary>Initializes a semaphore handle.</summary>
		/// <remarks>Use <see cref="Utility.WaitHandle(SemaphoreSlim)"/> instead.</remarks>
		/// <param name="semaphore">Semaphore to be released later.</param>
		internal SemaphoreHandle(SemaphoreSlim semaphore) => _semaphore = semaphore;

		/// <summary>Releases the semaphore once.</summary>
		public void Dispose() => _semaphore.Release();
	}
}
