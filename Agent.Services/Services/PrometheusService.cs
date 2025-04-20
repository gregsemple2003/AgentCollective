using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text; // Added for StringBuilder
using System.Text.Json;
using System.Threading.Tasks;
using System.Web; // Requires reference to System.Web or System.Web.HttpUtility in .NET Core/5+

namespace Agent.Services // Or your appropriate namespace
{
	//region Prometheus Common Response Classes
	// Classes to map the Prometheus API /api/v1/query_range response structure
	internal class PrometheusResponse
	{
		public string Status { get; set; }
		public PrometheusData Data { get; set; }
		public string ErrorType { get; set; }
		public string Error { get; set; }
		public List<string> Warnings { get; set; }
	}

	internal class PrometheusData
	{
		public string ResultType { get; set; } // "matrix", "vector", "scalar", "string"
		public List<PrometheusResult> Result { get; set; }
	}

	internal class PrometheusResult
	{
		public Dictionary<string, string> Metric { get; set; } // Labels
		public List<List<JsonElement>> Values { get; set; } // List of [timestamp, value] pairs for range query (matrix)
		public List<JsonElement> Value { get; set; } // Used for instant vector [timestamp, value]
	}
	//endregion

	//region Prometheus Series Response Classes
	// Classes to map the Prometheus API /api/v1/series response structure
	internal class PrometheusSeriesResponse
	{
		public string Status { get; set; }
		// Data is an array of label sets (dictionaries)
		public List<Dictionary<string, string>> Data { get; set; }
		public string ErrorType { get; set; }
		public string Error { get; set; }
		public List<string> Warnings { get; set; }
	}
	//endregion

	// Assuming DataFrame and Field classes are defined in this namespace or accessible
	// If not, include their definitions here as well (provided at the end).

	public class PrometheusService
	{
		private readonly HttpClient _client;
		private readonly string _prometheusApiUrl;
		private readonly string _bearerToken; // Store token for logging verification

		/// <summary>
		/// Initializes a new instance of the PrometheusService.
		/// </summary>
		/// <param name="baseAddress">The base URL of the Prometheus server (e.g., "https://last-epoch.gamefabric.dev/observability/metrics"). The '/api/v1/' path will be appended if necessary.</param>
		/// <param name="bearerToken">The bearer token for authentication.</param>
		public PrometheusService(string baseAddress, string bearerToken)
		{
			if (string.IsNullOrWhiteSpace(baseAddress))
				throw new ArgumentNullException(nameof(baseAddress));
			if (string.IsNullOrWhiteSpace(bearerToken))
				throw new ArgumentNullException(nameof(bearerToken));

			// *** Explicitly log the token being stored ***
			_bearerToken = bearerToken;
			Console.WriteLine($"DEBUG: Stored Bearer Token starts with: {_bearerToken.Substring(0, Math.Min(_bearerToken.Length, 4))}...");

			// Ensure baseAddress ends with /api/v1/ if not already provided like that
			var uri = new Uri(baseAddress);
			string apiPath = "/api/v1/";
			string fullBasePath;
			// Check if the path already ends with /api/v1 or /api/v1/
			if (uri.AbsolutePath.EndsWith(apiPath) || uri.AbsolutePath.EndsWith(apiPath.TrimEnd('/')))
			{
				// Use the path as is, ensuring it ends with a single slash
				fullBasePath = baseAddress.TrimEnd('/') + "/";
			}
			else // Assume /api/v1 needs to be appended
			{
				fullBasePath = baseAddress.TrimEnd('/') + apiPath;
				Console.WriteLine($"Warning: Appending {apiPath.TrimEnd('/')} to provided base address '{baseAddress}'. Resulting API base: '{fullBasePath}'");
			}
			_prometheusApiUrl = fullBasePath;


			var handler = new HttpClientHandler
			{
				AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
			};

			_client = new HttpClient(handler)
			{
				BaseAddress = new Uri(_prometheusApiUrl) // Use the processed path
			};

			Console.WriteLine($"Initializing PrometheusService. Base API URL: {_client.BaseAddress}");
			Console.WriteLine($"Using Bearer Token (verify format): Bearer {_bearerToken.Substring(0, Math.Min(_bearerToken.Length, 4))}...");

			_client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _bearerToken);

			// ** Modify/Add Headers to be more like curl **
			_client.DefaultRequestHeaders.Accept.Clear(); // Clear the default application/json
			_client.DefaultRequestHeaders.Accept.ParseAdd("*/*"); // Add */* like curl might send
																  //_client.DefaultRequestHeaders.Accept.ParseAdd("application/json"); // Re-add if */* alone doesn't work

			_client.DefaultRequestHeaders.UserAgent.ParseAdd("curl/7.64.1"); // Keep mimicking curl User-Agent
		}

		/// <summary>
		/// Executes an *instant* PromQL query ( /api/v1/query ).
		/// </summary>
		public async Task<List<DataFrame>> ExecuteInstantQueryAsync(string queryExpr, DateTime? evalTimeUtc = null)
		{
			var queryParams = System.Web.HttpUtility.ParseQueryString(string.Empty);
			queryParams["query"] = queryExpr;

			if (evalTimeUtc.HasValue)
				queryParams["time"] =
					evalTimeUtc.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

			var requestUri = $"query?{queryParams}";
			await LogHttpRequestDetailsAsync(HttpMethod.Get, requestUri, null, "Instant‑Query GET");

			HttpResponseMessage resp = null;
			try
			{
#if DEBUG
				{
					var baseUrl = new Uri(_client.BaseAddress, "query");   // /api/v1/query
					var sb = new System.Text.StringBuilder();

					sb.AppendLine("=== CURL EQUIVALENT (Linux) ===");
					sb.AppendLine($@"curl -s -G ""{baseUrl}"" \");
					sb.AppendLine($@"  -H ""Authorization: Bearer {_bearerToken}"" \");
					sb.AppendLine($@"  --data-urlencode ""query={queryExpr.Replace("\"", "\\\"")}"" \");

					if (evalTimeUtc.HasValue)
						sb.AppendLine($@"  --data-urlencode ""time={evalTimeUtc.Value
												   .ToUniversalTime()
												   .ToString("yyyy-MM-ddTHH:mm:ss.fffZ")}"" \");

					sb.AppendLine("  --compressed");
					sb.AppendLine("==============================");

					System.Diagnostics.Debug.WriteLine(sb.ToString());
				}
#endif



				resp = await _client.GetAsync(requestUri);
				var body = await resp.Content.ReadAsStringAsync();

				if (!resp.IsSuccessStatusCode)
				{
					TryParseAndThrowPrometheusError<PrometheusResponse>(body, queryExpr);
					resp.EnsureSuccessStatusCode();           // fallback throw
				}

				// Re‑use the range parser – it already understands “vector”.
				return ParsePrometheusRangeResponse(body);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Instant query failed: {ex.Message}");
				return new List<DataFrame>();
			}
			finally { resp?.Dispose(); }
		}

		/// <summary>
		/// Executes a PromQL query against the Prometheus range query API (/api/v1/query_range).
		/// </summary>
		/// <param name="startTime">Start time for the query range.</param>
		/// <param name="endTime">End time for the query range.</param>
		/// <param name="queryExpr">The PromQL query string.</param>
		/// <param name="step">The query resolution step width (e.g., "15s", "1m", "5m").</param>
		/// <returns>A list of DataFrames, one for each time series returned by Prometheus.</returns>
		public async Task<List<DataFrame>> ExecuteQueryRangeAsync(DateTime startTime, DateTime endTime, string queryExpr, string step)
		{
			// Prometheus API expects RFC3339 or Unix timestamp
			var startUtc = startTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
			var endUtc = endTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

			var queryParams = HttpUtility.ParseQueryString(string.Empty);
			queryParams["query"] = queryExpr;
			queryParams["start"] = startUtc;
			queryParams["end"] = endUtc;
			queryParams["step"] = step;

			// Relative path from BaseAddress
			var requestUri = $"query_range?{queryParams.ToString()}";

			// Log request before sending
			await LogHttpRequestDetailsAsync(HttpMethod.Get, requestUri, null, "Query Range GET");

			HttpResponseMessage response = null;
			try
			{
				response = await _client.GetAsync(requestUri);
				var responseData = await response.Content.ReadAsStringAsync(); // Read content for potential error details

				if (!response.IsSuccessStatusCode)
				{
					Console.WriteLine($"Error executing Prometheus query: {response.StatusCode}");
					Console.WriteLine($"Error details: {responseData}");
					TryParseAndThrowPrometheusError<PrometheusResponse>(responseData, queryExpr); // Use helper
					response.EnsureSuccessStatusCode(); // Fallback throw
				}

				Console.WriteLine("Prometheus query successful. Parsing response data...");
				return ParsePrometheusRangeResponse(responseData);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Exception during Prometheus range query request or parsing: {ex.Message}");
				return new List<DataFrame>(); // Return empty list on error
			}
			finally
			{
				response?.Dispose();
			}
		}

		/// <summary>
		/// Executes a PromQL query against the Prometheus range query API (/api/v1/query_range).
		/// </summary>
		/// <param name="startTime">Start time for the query range.</param>
		/// <param name="endTime">End time for the query range.</param>
		/// <param name="queryExpr">The PromQL query string.</param>
		/// <param name="step">The query resolution step width as a TimeSpan.</param>
		/// <returns>A list of DataFrames, one for each time series returned by Prometheus.</returns>
		public Task<List<DataFrame>> ExecuteQueryRangeAsync(DateTime startTime, DateTime endTime, string queryExpr, TimeSpan step)
		{
			string stepString;
			if (step.TotalSeconds < 1) stepString = $"{step.TotalMilliseconds}ms"; // Add milliseconds support
			else if (step.TotalSeconds < 60) stepString = $"{Math.Max(1, step.TotalSeconds):F0}s"; // Minimum 1s
			else if (step.TotalMinutes < 60) stepString = $"{Math.Max(1, step.TotalMinutes):F0}m"; // Minimum 1m
			else stepString = $"{Math.Max(1, step.TotalHours):F0}h"; // Minimum 1h

			return ExecuteQueryRangeAsync(startTime, endTime, queryExpr, stepString);
		}


		/// <summary>
		/// Executes a request against the Prometheus series API (/api/v1/series)
		/// to find time series matching the given label matchers.
		/// Tries POST first, then GET if POST fails with 401/405.
		/// </summary>
		/// <param name="matchers">A list of label matchers (e.g., 'up', 'process_cpu_seconds_total{job="myjob"}').</param>
		/// <param name="startTime">Optional start time (UTC) to limit the search range.</param>
		/// <param name="endTime">Optional end time (UTC) to limit the search range.</param>
		/// <returns>A list of dictionaries, where each dictionary represents the label set of a matching series.</returns>
		public async Task<List<Dictionary<string, string>>> ExecuteSeriesQueryAsync(List<string> matchers, DateTime? startTime = null, DateTime? endTime = null)
		{
			if (matchers == null || !matchers.Any())
			{
				throw new ArgumentException("At least one matcher must be provided.", nameof(matchers));
			}

			var requestUriPath = "series"; // Relative path

			// --- Try POST first ---
			HttpResponseMessage response = null;
			HttpContent initialContentForLogging = null; // Content used for logging
			HttpContent actualContentToSend = null;      // Content actually sent
			string responseContent = null;
			bool tryGet = false;
			string postBody = null; // To store the read body

			Console.WriteLine($"DEBUG: Using token starting with '{_bearerToken.Substring(0, Math.Min(_bearerToken.Length, 4))}' for POST request.");

			try
			{
				// 1. Prepare initial data
				var formData = new List<KeyValuePair<string, string>>();
				foreach (var matcher in matchers)
				{
					formData.Add(new KeyValuePair<string, string>("match[]", matcher));
				}
				// ** Temporarily disable time params for debugging 401 **
				// AddOptionalTimeParams(formData, startTime, endTime);
				Console.WriteLine("DEBUG: Time parameters temporarily disabled for Series POST.");

				// 2. Create content JUST for logging (will be consumed)
				initialContentForLogging = new FormUrlEncodedContent(formData);

				// 3. Log the details, reading the body from initialContentForLogging
				// Note: Modify LogHttpRequestDetailsAsync if you want it to *return* the body string instead of reading twice
				await LogHttpRequestDetailsAsync(HttpMethod.Post, requestUriPath, initialContentForLogging, "POST Attempt");
				postBody = await initialContentForLogging.ReadAsStringAsync(); // Read the body string needed for actual content

				// 4. Create the ACTUAL content to be sent using the read body string
				if (postBody != null)
				{
					// Ensure content type matches what FormUrlEncodedContent would use
					actualContentToSend = new StringContent(postBody, Encoding.UTF8, "application/x-www-form-urlencoded");
				}
				else
				{
					actualContentToSend = new StringContent("", Encoding.UTF8, "application/x-www-form-urlencoded");
					Console.WriteLine("Warning: POST body for logging was null, sending empty StringContent.");
				}

				// 5. Send the request using the actual content
				response = await _client.PostAsync(requestUriPath, actualContentToSend);
				responseContent = await response.Content.ReadAsStringAsync(); // Read body for logging/parsing

				if (response.IsSuccessStatusCode)
				{
					Console.WriteLine($"POST successful (Status: {response.StatusCode}). Response Body Preview: {responseContent.Substring(0, Math.Min(responseContent.Length, 500))}...");
					return ParsePrometheusSeriesResponse(responseContent);
				}
				else
				{
					Console.WriteLine($"POST request failed: {response.StatusCode}"); // Should be 401
					Console.WriteLine($"POST Response Body: {responseContent}");

					if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.MethodNotAllowed)
					{
						Console.WriteLine("POST failed with 401/405, attempting GET...");
						tryGet = true;
					}
					else
					{
						TryParseAndThrowPrometheusError<PrometheusSeriesResponse>(responseContent, $"POST {string.Join(", ", matchers)}");
						response.EnsureSuccessStatusCode();
						return new List<Dictionary<string, string>>();
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Exception during Prometheus series POST request: {ex.Message}");
				if (response?.StatusCode == HttpStatusCode.Unauthorized || response?.StatusCode == HttpStatusCode.MethodNotAllowed) { tryGet = true; }
				else { return new List<Dictionary<string, string>>(); }
			}
			finally
			{
				// Only dispose if NOT falling back to GET
				if (!tryGet)
				{
					response?.Dispose();
					initialContentForLogging?.Dispose();
					actualContentToSend?.Dispose();
				}
			}


			// --- Fallback to GET ---
			if (tryGet)
			{
				// Dispose resources from POST attempt before trying GET
				response?.Dispose();
				initialContentForLogging?.Dispose();
				actualContentToSend?.Dispose();
				response = null; // Reset response variable
				responseContent = null;

				Console.WriteLine($"DEBUG: Using token starting with '{_bearerToken.Substring(0, Math.Min(_bearerToken.Length, 4))}' for GET request.");

				try
				{
					var queryParams = HttpUtility.ParseQueryString(string.Empty);
					foreach (var matcher in matchers) { queryParams.Add("match[]", matcher); }
					// ** Temporarily disable time params for debugging 401 **
					// AddOptionalTimeParams(queryParams, startTime, endTime);
					Console.WriteLine("DEBUG: Time parameters temporarily disabled for Series GET.");

					var getRequestUri = $"{requestUriPath}?{queryParams.ToString()}";

					// Log GET details (no body content)
					await LogHttpRequestDetailsAsync(HttpMethod.Get, getRequestUri, null, "GET Attempt");

					response = await _client.GetAsync(getRequestUri);
					responseContent = await response.Content.ReadAsStringAsync(); // Read body first
					Console.WriteLine($"GET Response Status: {response.StatusCode}"); // Should be 401
					Console.WriteLine($"GET Raw Response Body: {responseContent}"); // Log the 401 body

					if (!response.IsSuccessStatusCode)
					{
						Console.WriteLine($"GET request failed (after reading body): {response.StatusCode}");
						if (response.StatusCode == HttpStatusCode.Unauthorized)
						{
							Console.WriteLine("!!! GET request resulted in 401 Unauthorized. Check token, URL, and potentially network/proxy settings.");
							Console.WriteLine("!!! Compare logged C# request details above with the working curl --trace-ascii output.");
							return new List<Dictionary<string, string>>();
						}
						TryParseAndThrowPrometheusError<PrometheusSeriesResponse>(responseContent, $"GET {string.Join(", ", matchers)}");
						response.EnsureSuccessStatusCode(); // Throw for other errors
					}

					// This part should likely not be reached if status was 401
					Console.WriteLine("GET HTTP request successful. Attempting to parse response data...");
					return ParsePrometheusSeriesResponse(responseContent);
				}
				catch (JsonException jsonEx) // Catch JSON specific exceptions if status was OK but body invalid
				{
					Console.WriteLine($"!!! JSON Parsing Failed after GET request: {jsonEx.Message}");
					Console.WriteLine($"!!! Verify the GET Raw Response Body logged above is valid JSON matching PrometheusSeriesResponse structure.");
					return new List<Dictionary<string, string>>();
				}
				catch (Exception ex) { Console.WriteLine($"Exception during Prometheus series GET request or parsing: {ex.Message}"); }
				finally { response?.Dispose(); }
			}

			Console.WriteLine("Both POST and GET attempts for series query failed, likely due to 401 Unauthorized.");
			Console.WriteLine("--> Verify token is identical to the one used in curl.");
			Console.WriteLine("--> Check for proxy/firewall interference.");
			Console.WriteLine("--> Compare logged C# request details with curl --trace-ascii output.");
			return new List<Dictionary<string, string>>();
		}


		// --- Helper to add time parameters ---
		private void AddOptionalTimeParams(List<KeyValuePair<string, string>> formData, DateTime? startTime, DateTime? endTime)
		{
			if (startTime.HasValue)
			{
				var startUtc = startTime.Value.Kind == DateTimeKind.Unspecified
					? DateTime.SpecifyKind(startTime.Value, DateTimeKind.Utc)
					: startTime.Value.ToUniversalTime();
				formData.Add(new KeyValuePair<string, string>("start", new DateTimeOffset(startUtc).ToUnixTimeSeconds().ToString()));
			}
			if (endTime.HasValue)
			{
				var endUtc = endTime.Value.Kind == DateTimeKind.Unspecified
					? DateTime.SpecifyKind(endTime.Value, DateTimeKind.Utc)
					: endTime.Value.ToUniversalTime();
				formData.Add(new KeyValuePair<string, string>("end", new DateTimeOffset(endUtc).ToUnixTimeSeconds().ToString()));
			}
		}
		// Overload for NameValueCollection (used by HttpUtility.ParseQueryString)
		private void AddOptionalTimeParams(System.Collections.Specialized.NameValueCollection queryParams, DateTime? startTime, DateTime? endTime)
		{
			if (startTime.HasValue)
			{
				var startUtc = startTime.Value.Kind == DateTimeKind.Unspecified
				   ? DateTime.SpecifyKind(startTime.Value, DateTimeKind.Utc)
				   : startTime.Value.ToUniversalTime();
				queryParams.Add("start", new DateTimeOffset(startUtc).ToUnixTimeSeconds().ToString());
			}
			if (endTime.HasValue)
			{
				var endUtc = endTime.Value.Kind == DateTimeKind.Unspecified
				   ? DateTime.SpecifyKind(endTime.Value, DateTimeKind.Utc)
				   : endTime.Value.ToUniversalTime();
				queryParams.Add("end", new DateTimeOffset(endUtc).ToUnixTimeSeconds().ToString());
			}
		}

		// --- Parsing Methods ---

		/// <summary>
		/// Parses the JSON response from a Prometheus /api/v1/query_range query.
		/// </summary>
		private List<DataFrame> ParsePrometheusRangeResponse(string responseData)
		{
			var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			var prometheusResponse = JsonSerializer.Deserialize<PrometheusResponse>(responseData, options);

			if (prometheusResponse?.Status != "success")
			{
				Console.WriteLine($"Prometheus query failed or response format unknown. Status: {prometheusResponse?.Status}, Error: {prometheusResponse?.Error}");
				return new List<DataFrame>();
			}
			if (prometheusResponse.Data?.Result == null)
			{
				Console.WriteLine("Prometheus response data or result is null.");
				return new List<DataFrame>();
			}
			if (prometheusResponse.Data.ResultType != "matrix")
			{
				Console.WriteLine($"Warning: Expected resultType 'matrix' but got '{prometheusResponse.Data.ResultType}'. Processing may fail.");
				// Handle vector or other types if needed, otherwise return empty or let it fail later.
				if (prometheusResponse.Data.ResultType != "vector") // Basic handling for vector if needed
				{
					return new List<DataFrame>();
				}
			}

			var dataFrames = new List<DataFrame>();
			foreach (var promResult in prometheusResponse.Data.Result)
			{
				// Handle both matrix (Values) and potentially vector (Value) results slightly differently
				bool isMatrix = promResult.Values != null;
				var valuePairs = isMatrix ? promResult.Values : (promResult.Value != null ? new List<List<JsonElement>> { promResult.Value } : null);

				if (valuePairs == null || !valuePairs.Any()) continue;

				var timeValues = new List<DateTime>();
				var numberValues = new List<double>();

				foreach (var valuePair in valuePairs)
				{
					if (valuePair.Count == 2 &&
						valuePair[0].ValueKind == JsonValueKind.Number &&
						valuePair[1].ValueKind == JsonValueKind.String)
					{
						var timestamp = valuePair[0].GetDouble();
						var dt = DateTimeOffset.FromUnixTimeSeconds((long)timestamp)
											  .AddSeconds(timestamp - Math.Truncate(timestamp))
											  .UtcDateTime;
						timeValues.Add(dt);

						if (double.TryParse(valuePair[1].GetString(), NumberStyles.Float | NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out double numValue))
						{
							numberValues.Add(numValue);
						}
						else if (valuePair[1].GetString()?.ToLowerInvariant() == "nan")
						{
							numberValues.Add(double.NaN); // Handle NaN explicitly
						}
						else
						{
							numberValues.Add(double.NaN); // Default to NaN on parse failure
							Console.WriteLine($"Warning: Could not parse Prometheus value '{valuePair[1].GetString()}' as double for series {LabelsToString(promResult.Metric)}");
						}
					}
					else { Console.WriteLine($"Warning: Unexpected format in Prometheus value pair: {valuePair.Count} elements."); }
				}

				// Create DataFrame structure
				var timeField = new Field { Name = "Time", Type = "time", CSharpType = typeof(DateTime) };
				var valueField = new Field { Name = "Value", Type = "number", CSharpType = typeof(double), Labels = promResult.Metric ?? new Dictionary<string, string>() };

				dataFrames.Add(new DataFrame
				{
					Fields = new List<Field> { timeField, valueField },
					Values = new List<IList> { timeValues, numberValues }
				});
			}
			return dataFrames;
		}

		/// <summary>
		/// Parses the JSON response from a Prometheus /api/v1/series query.
		/// </summary>
		private List<Dictionary<string, string>> ParsePrometheusSeriesResponse(string responseData)
		{
			var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			var seriesResponse = JsonSerializer.Deserialize<PrometheusSeriesResponse>(responseData, options);

			if (seriesResponse?.Status != "success")
			{
				Console.WriteLine($"Prometheus series query failed or response format unknown. Status: {seriesResponse?.Status}, Error: {seriesResponse?.Error}");
				return new List<Dictionary<string, string>>();
			}
			if (seriesResponse.Data == null)
			{
				Console.WriteLine("Prometheus series response data is null.");
				return new List<Dictionary<string, string>>();
			}
			return seriesResponse.Data;
		}


		// --- Helper Methods ---

		/// <summary>
		/// Helper to convert label dictionary to a string representation.
		/// </summary>
		private string LabelsToString(Dictionary<string, string> labels)
		{
			if (labels == null || !labels.Any()) return "{}";
			return $"{{{string.Join(", ", labels.Select(kv => $"{kv.Key}=\"{kv.Value}\""))}}}";
		}

		/// <summary>
		/// Helper to log the default request headers.
		/// </summary>
		private void LogRequestHeaders(HttpClient client, string method)
		{
			var sb = new StringBuilder();
			sb.AppendLine($"--- Default {method} Request Headers ---");
			foreach (var header in client.DefaultRequestHeaders)
			{
				// Special handling for Authorization to avoid logging the full token
				if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) && header.Value.Any())
				{
					var authValue = header.Value.First();
					if (authValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
					{
						sb.AppendLine($"{header.Key}: Bearer {_bearerToken.Substring(0, Math.Min(_bearerToken.Length, 4))}...");
					}
					else
					{
						sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}"); // Log other auth types fully (if any)
					}
				}
				else
				{
					sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
				}
			}
			sb.AppendLine("---------------------------------");
			Console.WriteLine(sb.ToString());
		}

		/// <summary>
		/// Logs the details of an intended HTTP request for debugging, mimicking raw format.
		/// IMPORTANT: If content is provided, it reads the content stream, which might consume it.
		/// Ensure the original content can be re-read or create new content for the actual request.
		/// </summary>
		private async Task LogHttpRequestDetailsAsync(HttpMethod method, string relativeOrAbsoluteUri, HttpContent content = null, string context = "")
		{
			var sb = new StringBuilder();
			var requestUri = new Uri(_client.BaseAddress, relativeOrAbsoluteUri); // Ensure absolute URI

			sb.AppendLine($"--- {context} Raw Request Details ---");
			// Request Line
			sb.AppendLine($"{method.Method} {requestUri.PathAndQuery} HTTP/1.1"); // Assuming HTTP/1.1 for logging clarity

			// Host Header (Essential)
			sb.AppendLine($"Host: {requestUri.Host}");

			// Default Headers from HttpClient (including Authorization)
			sb.AppendLine("--- Default Headers ---");
			foreach (var header in _client.DefaultRequestHeaders)
			{
				// Log Authorization header carefully
				if (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) && header.Value.Any())
				{
					var authValue = header.Value.First();
					if (authValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
					{
						sb.AppendLine($"{header.Key}: Bearer {_bearerToken.Substring(0, Math.Min(_bearerToken.Length, 4))}... (Full token hidden)");
						// Optionally log the full token ONLY if absolutely necessary for debugging, then remove:
						// sb.AppendLine($"!!! DEBUG ONLY - Full Auth: {header.Key}: {string.Join(", ", header.Value)}");
					}
					else { sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}"); }
				}
				else { sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}"); }
			}

			// Content-Specific Headers
			string body = null;
			if (content != null)
			{
				sb.AppendLine("--- Content Headers ---");
				foreach (var header in content.Headers)
				{
					sb.AppendLine($"{header.Key}: {string.Join(", ", header.Value)}");
				}
				// Read the body (stream consuming!)
				try
				{
					// Load into buffer first to allow multiple reads if needed by logger AND actual request
					// await content.LoadIntoBufferAsync(); // Use this if you need to reuse the *same* HttpContent object

					// Read the stream to get the body string for logging
					body = await content.ReadAsStringAsync();
					// Calculate length based on actual read body, Content-Length header might be deferred
					sb.AppendLine($"Content-Length: {Encoding.UTF8.GetBytes(body).Length}"); // Calculate length from string
				}
				catch (Exception ex)
				{
					sb.AppendLine($"!!! Error reading request body for logging: {ex.Message}");
				}
			}

			// Blank line separating headers and body
			sb.AppendLine();

			// Body
			if (body != null)
			{
				sb.AppendLine(body);
			}
			else if (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch)
			{
				sb.AppendLine("[No Request Body Content Provided/Read]");
			}


			sb.AppendLine($"--- End {context} Raw Request Details ---");
			Console.WriteLine(sb.ToString());
		}


		/// <summary>
		/// Helper to attempt parsing a Prometheus error response and throw a specific exception.
		/// </summary>
		private void TryParseAndThrowPrometheusError<T>(string errorContent, string context) where T : class
		{
			// Basic check for Prometheus error structure (adjust properties if needed)
			if (typeof(T) == typeof(PrometheusResponse) || typeof(T) == typeof(PrometheusSeriesResponse))
			{
				try
				{
					var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
					// Use dynamic to access common error properties without casting to specific type first
					using var jsonDoc = JsonDocument.Parse(errorContent);
					var root = jsonDoc.RootElement;

					// Check if the necessary properties exist and have values
					if (root.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String &&
						statusElement.GetString() == "error" &&
						root.TryGetProperty("error", out var errorElement) && errorElement.ValueKind == JsonValueKind.String)
					{
						string error = errorElement.GetString();
						if (!string.IsNullOrEmpty(error))
						{
							string errorType = "Unknown";
							if (root.TryGetProperty("errorType", out var errorTypeElement) && errorTypeElement.ValueKind == JsonValueKind.String)
							{
								errorType = errorTypeElement.GetString();
							}
							throw new HttpRequestException($"Prometheus API Error ({errorType}): {error}. Context: {context}");
						}
					}
				}
				catch (JsonException) { /* Ignore if parsing fails, fallback to EnsureSuccessStatusCode */ }
				catch (InvalidOperationException) { /* Ignore if properties don't exist, fallback */ }
			}
		}
	}


	// --- Required Supporting Classes (If not defined elsewhere) ---

	// Minimal DataFrame and Field definitions to match usage
	// (Expand with properties from original GrafanaService if needed)
	public class DataFrame
	{
		public List<Field> Fields { get; set; } = new List<Field>();
		public List<IList> Values { get; set; } = new List<IList>(); // Each IList is a List<T>

		// Example helper properties (adjust based on original DataFrame)
		public Dictionary<string, string> Dimensions => GetValueField()?.Labels ?? new Dictionary<string, string>();
		public List<double> Series => Values.Count > 1 && Values[1] is List<double> ? (List<double>)Values[1] : new List<double>();
		public List<DateTime> Timestamps => Values.Count > 0 && Values[0] is List<DateTime> ? (List<DateTime>)Values[0] : new List<DateTime>();

		private Field GetValueField() => Fields?.FirstOrDefault(f => f.Type == "number");

		public Dictionary<DateTime, double> AsDictionary()
		{
			var times = Timestamps;
			var vals = Series;
			if (times.Count != vals.Count || times.Count == 0) return new Dictionary<DateTime, double>();
			// Handle potential duplicate timestamps if Prometheus returns them (though unlikely for range query)
			return Enumerable.Range(0, times.Count)
							 .GroupBy(i => times[i])
							 .ToDictionary(g => g.Key, g => vals[g.First()]); // Take first value for duplicate times
		}
	}

	public class Field
	{
		public string Name { get; set; }
		public string Type { get; set; } // e.g., "time", "number"
		public Type CSharpType { get; set; }
		public Dictionary<string, string> Labels { get; set; } // Holds Prometheus labels for value fields
															   // Add other properties like TypeInfo, Config if needed from original GrafanaService version
	}
}