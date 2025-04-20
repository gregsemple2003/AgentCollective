// Utils/ScopedTimer.cs
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Agent.Services.Utils
{
	/// <summary>
	/// Usage:
	/// <code>
	/// using (ScopedTimer.Measure(new { clusterName, dayStart }))
	/// {
	///     await CalculateAndStoreDailyPeakCpu(...);
	/// }
	/// </code>
	/// Console output example:
	/// <c>CalculateDailyPeaks completed in 342 ms  |  args={"clusterName":"ni-prod-nlams-sco01","dayStart":"2025‑04‑20"}</c>
	/// </summary>
	public readonly struct ScopedTimer : IDisposable
	{
		private readonly Stopwatch _sw;
		private readonly string _caller;
		private readonly string _jsonArgs;
		private readonly Action<string> _sink;

		private ScopedTimer(string caller, object args, Action<string> sink)
		{
			_caller = caller;
			_sink = sink ?? Console.WriteLine;

			// Serialize args once, nothing if null
			_jsonArgs = args is null ? "{}"
						: JsonSerializer.Serialize(
							  args,
							  new JsonSerializerOptions { WriteIndented = false });

			Console.WriteLine($"{_caller} started |  args={_jsonArgs}");

			// Latch timestamp after we log
			_sw = Stopwatch.StartNew();
		}

		/// <summary>
		/// Begin a timed scope.
		/// </summary>
		/// <param name="args">Anonymous or POCO with the parameters you want logged.</param>
		/// <param name="caller">
		/// Filled automatically by the compiler; do not pass explicitly.
		/// </param>
		/// <param name="sink">
		/// Optional log sink; defaults to <c>Console.WriteLine</c>.
		/// </param>
		public static ScopedTimer Measure(
			object args = null,
			[CallerMemberName] string caller = "",
			Action<string> sink = null)
		{
			return new ScopedTimer(caller, args, sink);
		}

		public void Dispose()
		{
			_sw.Stop();
			_sink($"{_caller} completed in {_sw.ElapsedMilliseconds} ms  |  args={_jsonArgs}");
		}
	}
}
