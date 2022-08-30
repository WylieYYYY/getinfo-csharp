#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WylieYYYY.GetinfoCSharp
{
	/// <summary>General non-domain-specific utilities.</summary>
	public static class Utility
	{
		#pragma warning disable CS1998
		/// <summary>Gets an empty asynchronous enumerator of a given value type.</summary>
		/// <returns>Empty asynchronous enumerator.</returns>
		public static async IAsyncEnumerator<TValue> EmptyAsyncEnumerator<TValue>() { yield break; }
		#pragma warning restore CS1998
		/// <summary>
		///  Retry executing <paramref name="func"/> by <paramref name="maxAttempts"/> times.
		///  Ends by either <paramref name="maxAttempts"/> times of attempt are done and exception
		///  still occurs, <paramref name="func"/> returns without exception,
		///  or <paramref name="beforeRetry"/> or <paramref name="exceptionFilter"/> throws.
		/// </summary>
		/// <param name="func">Function to be retried and get value from.</param>
		/// <param name="maxAttempts">Maximum amount to attempts, default is 0 for unlimited.</param>
		/// <param name="beforeRetry">
		///  Asynchronous function to be executed before each retry,
		///  default is null for not doing anything. This function's exceptions are unchecked.
		/// </param>
		/// <param name="exceptionFilter">
		///  Additional filter for selecting exception,
		///  default is null for no additional checks beyond type.
		/// </param>
		/// <returns>Asynchronous value returned by <paramref name="func"/>.</returns>
		public static async Task<TValue> AttemptRetry<TValue, TException>(Func<Task<TValue>> func,
				int maxAttempts = 0, Func<Task>? beforeRetry = null,
				Func<TException, bool>? exceptionFilter = null) where TException : Exception
		{
			for (int attemptCount = 1; true; attemptCount++)
			{
				try { return await func(); }
				catch (TException ex) when (exceptionFilter?.Invoke(ex) ?? true)
				{
					if (attemptCount == maxAttempts) throw;
					else await (beforeRetry?.Invoke() ?? Task.CompletedTask);
				}
			}
		}
		/// <summary>
		///  Yields returned value as soon as any task has completed,
		///  and removes the task from the list of tasks, repeats until all tasks are completed.
		/// </summary>
		/// <param name="tasks">List of tasks to be completed.</param>
		/// <returns>Asynchronous enumerator of values returned by the tasks.</returns>
		public static async IAsyncEnumerator<TValue> UnrollCompletedTasks<TValue>(
				this List<Task<TValue>> tasks)
		{
			while (tasks.Any())
			{
				Task<TValue> completedTask = await Task.WhenAny(tasks);
				tasks.Remove(completedTask);
				yield return await completedTask;
			}
		}
	}
}
