using CsvHelper;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Collections;
using Agent.Services;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace GrafanaServiceExample
{
    public class DataFrame
    {
        public List<Field> Fields { get; set; }
        public List<IList> Values { get; set; } // Each IList is a List<T> based on field type

        // Useful when dealing with a single time series, which is typical for many queries.  Dimension aka column, aka label in Prometheus.
        public string DimensionName => GetFirstNumberField().Labels.FirstOrDefault().Key;
        public string DimensionValue => GetFirstNumberField().Labels.FirstOrDefault().Value;
        public List<double> Series
        {
            get
            {
                GetFirstNumberField();
                return _cachedDataSeries;
            }
        }

        public Dictionary<string, string> Dimensions
        {
            get
            {
                GetFirstNumberField();
                return _cachedNumberField.Labels;
            }
        }

        public Dictionary<DateTime, double> AsDictionary()
        {
            // Find the time field
            var timeField = Fields.FirstOrDefault(f => f.Type == "time");
            if (timeField == null)
            {
                throw new InvalidOperationException("No time field found in the DataFrame.");
            }

            // Find the number field
            var numberField = GetFirstNumberField();
            if (numberField == null)
            {
                throw new InvalidOperationException("No number field found in the DataFrame.");
            }

            var timeIndex = Fields.IndexOf(timeField);
            var numberIndex = Fields.IndexOf(numberField);

            // Retrieve the time and number values
            var timeValues = Values[timeIndex] as List<DateTime>;
            var numberValues = Values[numberIndex] as List<double>;

            if (timeValues == null || numberValues == null)
            {
                throw new InvalidOperationException("Time or number values are not in the expected format.");
            }

            if (timeValues.Count != numberValues.Count)
            {
                throw new InvalidOperationException("Time and number values lists have different counts.");
            }

            // Create the dictionary by zipping the time and number values
            var result = new Dictionary<DateTime, double>();
            for (int i = 0; i < timeValues.Count; i++)
            {
                result[timeValues[i]] = numberValues[i];
            }

            return result;
        }

        private List<double> _cachedDataSeries;
        private Field _cachedNumberField;

        private Field GetFirstNumberField()
        {
            // Return the cached KeyValuePair if it has already been calculated
            if (_cachedNumberField != null)
            {
                return _cachedNumberField;
            }

            // Find the first field with Type "number"
            var numberField = Fields.FirstOrDefault(f => f.Type == "number");
            if (numberField != null)
            {
                _cachedNumberField = numberField;
            }

            var numberIndex = Fields.IndexOf(numberField);
            _cachedDataSeries = (List<double>)Values[numberIndex];

            return _cachedNumberField;
        }

    }

    // Classes to map the JSON structure
    public class Root
    {
        public Dictionary<string, Result> Results { get; set; }
    }

    public class Result
    {
        public int Status { get; set; }
        public List<Frame> Frames { get; set; }
    }

    public class Frame
    {
        public Schema Schema { get; set; }
        public Data Data { get; set; }
    }

    public class Schema
    {
        public string RefId { get; set; }
        public Meta Meta { get; set; }
        public List<Field> Fields { get; set; }
    }

    public class Meta
    {
        public string Type { get; set; }
        public List<int> TypeVersion { get; set; }
        public Custom Custom { get; set; }
        public string ExecutedQueryString { get; set; }
    }

    public class Custom
    {
        public string ResultType { get; set; }
    }

    public class Field
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public Type CSharpType { get; set; }
        public TypeInfo TypeInfo { get; set; }
        public Config Config { get; set; }
        public Dictionary<string, string> Labels { get; set; }
    }

    public class TypeInfo
    {
        public string Frame { get; set; }
    }

    public class Config
    {
        public int? Interval { get; set; }
    }

    public class Data
    {
        public List<JsonElement> Values { get; set; }
    }

    public class DataSource
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Uid { get; set; }
        public string Url { get; set; }
    }

    public class GrafanaService
    {
        private readonly HttpClient _client;

        public GrafanaService()
        {
            var handler = new HttpClientHandler
            {
                // Enable automatic decompression for gzip, deflate, and br
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            };

            _client = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://last-epoch.gamefabric.dev")
            };
        }

        private HttpRequestMessage CreateRequestMessage(HttpMethod method, string requestUri)
        {
            var request = new HttpRequestMessage(method, requestUri);

            // Set headers as specified in the raw HTTP request
            request.Headers.Host = "last-epoch.gamefabric.dev";
            request.Headers.Connection.Add("keep-alive");
            request.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.Zero };
            request.Headers.Add("sec-ch-ua", "\"Chromium\";v=\"130\", \"Google Chrome\";v=\"130\", \"Not?A_Brand\";v=\"99\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.Add("Upgrade-Insecure-Requests", "1");
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            request.Headers.Add("Sec-Fetch-Site", "cross-site");
            request.Headers.Add("Sec-Fetch-Mode", "navigate");
            request.Headers.Add("Sec-Fetch-User", "?1");
            request.Headers.Add("Sec-Fetch-Dest", "document");
            request.Headers.AcceptEncoding.ParseAdd("gzip, deflate, br, zstd");
            request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

            // Include the full Cookie header from your raw request
            request.Headers.Add("Cookie", "GCP_IAP_UIDGCP_IAP_UID=114413497210153258398; fwdauth-session=2oTwBCCWTv44st2e7hs3vI7ZDTU; __Host-GCP_IAP_AUTH_TOKEN_7A9905A94A9023D7=AeuTOJ_xg_AQLOC0g6CTzo2zh0c0mQo4M7B-D2kKDbjpc5ZVae1Y5kUmgYBdaG4ryoD3rvSOCuGe1XRiYLhClfa1dHDftz1J28jBLiIj79ibaoMqs2xIUmiIeFoNc4YjRq-IDdmlet-bx0Sn0RhfVKKPJ7Fh3HdmIxOQTMhiKm6s-hb0S4FYCe1sTbyru3OjqcFflRqSN8i9nML_TKdJmaiCATf_1lvj_Ld4t0reHk2NbcvNxhl2LV8L1s2nx55xzDPCwHU6vUYAsb-s90pogPBVjlcpnsW6PFeimYRp5MOdzH9vPJnxEmk8EcJ90Z_a2bzcEQEHNgQhvrVBCPvRgGfTAjKe3fBHUgL-NXU1wrdkVIiYxWUDO4Llw2xVN3m5q4IdDwVex9g0lt1Gk9ytcc5WLuq-vR5OvdpspO6YO0Oyc5xMBKwkJWT6TWL81_WTMmCehnRB_bpB5bArbkzw4C9nCW9jQp5FiXl8YdwlBaxrdSJ2DL4dOoOkhHteBAfo2tCNXWaH-BaX0xz3527ADcm-VsmELaeiB3wvFKBQqmHkzBgq3GGB5nw4zJLwZswkjXgW1bSTmbnJvbW9fM3nqYVGnx8o1xxLY8LSot_z8tsFVV8IiuPB2Xgr1XvlCnbZ8422eDJ9Dq0-6Y3catDjKI34_EMjGzMBnhgEFGwX7-DpArO66wFwU8xFt9199c4TRqiElHVr0jMCU02q5KvV1lSoRhyIqOP0iT3JnvWB0dzXrBbXTJwkMiDWN0OsqLDVuyJ1jaBW-9cvTzY98KFC_Jg5ZP0GxVqxwATyUMP5Rs5I2zW6eBaAprGmanz4eFIjpOnGr6g7bTp9Eh5DfwnMSDWxYHtqHy7uWIBbpP9oXUKgvAYggd4m9-T6oVXlVAfpmQxUTGH7VY0rhI3jBv0Mr6JZW7Kzqws_Emim0VwRlnkvRqL4GBV-qDV7cigfic58sDQt0fxaKZ1sTPsCqjQ0JCMICoMzZ2sRf9nceYAetI104Ld-zQlO4FUNkXFFOYQ5VI1Qg144qbvkttK1p4ur4T57xXA2bjszWEzf7H0RjyvXCa_m3_HZ7gG-STAgiyENYaM-lK4AzAjAQACVD82Esnr4vI2A6oUNrFaOf86BuqdvnBzEFgGBh_Ql5l21clMh3vw-bAxu48fthN_-qPBsc_PjB5AUWEyd-PMbmUKrpyrK4t3fbyfsi-cw7yr36yA5pF0ZRoqUzeXxhuglcR-Tb1ehrKIcrnH6u5-TXIzm4IVsNvXh-ai069-AlVxt3QPFLMPsEYgLpCzS5bnp6-w-iyUcxK8yK9qL6JcuX1uBgP_8zscru6uUOjCS2tji6JbjhGD34DFPToflvCxU4LJj6O8EC01qVCFpjUHvCxG4CJZ0r1-0noLIxo6GEiLk7PiH-spcHMsRASR1PAPvXLaYAmr_t9nyPRPOG8rBCcTejCpzFQOrwyKwAR7G5augOmSTK2FA0uCDtDMMfjhmCOpBLOzVhcSjpccoarLG3h6_-v2z9D-kR_eTDJFPBtSSsOjNBG8xn8xO-2kz6ptPiOTIprvHNFgQ-VCDc_-IMtJEBHZJYLRCIq6kaHnjUujfgx9_DUW4CiqyytvCICjMu3EcWYJTEwE4XAqtM4p7499_nGj9q0I9JMENTW7gRvE3ErrQVL0hn0Kn8_L2Ls0gHOSWKxVlGIhEbhqL1T7AQ0mgVmX9heOl6Bi_9y4fu88tQGF6fSvoz1AXbT83bbdX0fbsiLiEEqk3nDq2F78L0qWmNROqDDHQIKLolJPDTv5E9xkK");

            return request;
        }

        public async Task<DataSource> GetDataSourceAsync(string datasourceName)
        {
            var requestUri = "/monitoring/api/datasources";
            var request = CreateRequestMessage(HttpMethod.Get, requestUri);

            // Send the request
            HttpResponseMessage response = await _client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error retrieving data sources: {response.StatusCode}");
                Console.WriteLine($"Error details: {await response.Content.ReadAsStringAsync()}");
                return null;
            }

            var datasources = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
            foreach (var ds in datasources.EnumerateArray())
            {
                if (ds.GetProperty("name").GetString().Equals(datasourceName, StringComparison.OrdinalIgnoreCase))
                {
                    // Map the JSON data to the DataSource object
                    return new DataSource
                    {
                        Id = ds.GetProperty("id").GetInt32(),
                        Name = ds.GetProperty("name").GetString(),
                        Type = ds.GetProperty("type").GetString(),
                        Uid = ds.GetProperty("uid").GetString(),
                        Url = ds.GetProperty("url").GetString()
                        // Map other properties as needed
                    };
                }
            }

            return null; // Data source not found
        }

        public async Task<List<DataFrame>> ExecuteQueryAsync(string datasourceName, DateTime startTime, string queryExpr)
        {
            var datasource = await GetDataSourceAsync(datasourceName);
            if (datasource == null)
            {
                Console.WriteLine($"Data source '{datasourceName}' not found.");
                return null;
            }

            var requestId = Guid.NewGuid().ToString();
            var utcOffsetSec = Math.Round((DateTime.Now - DateTime.UtcNow).TotalSeconds);
            var to = DateTime.UtcNow;
            var from = startTime; // Adjust the time range as needed
            long toUnixTime = new DateTimeOffset(to).ToUnixTimeMilliseconds();
            long fromUnixTime = new DateTimeOffset(from).ToUnixTimeMilliseconds();

            var requestBody = new
            {
                queries = new[]
                {
                    new
                    {
                        datasource = new
                        {
                            type = datasource.Type,
                            uid = datasource.Uid
                        },
                        editorMode = "code", // we are providing a query expression, e.g. code
                        expr = queryExpr,
                        interval = "",
                        legendFormat = "{{region}}",
                        range = true,
                        refId = "A", // unique name for each query
                        exemplar = false,
                        requestId = requestId,
                        utcOffsetSec = utcOffsetSec,
                        datasourceId = datasource.Id,
                        intervalMs = 15000,
                        maxDataPoints = 1187
                    }
                },
                from = fromUnixTime.ToString(),
                to = toUnixTime.ToString()
            };

            var requestUri = "/monitoring/api/ds/query";
            var request = CreateRequestMessage(HttpMethod.Post, requestUri);

            // Set the content for the POST request
            var options = new JsonSerializerOptions { IgnoreNullValues = true };
            var content = new StringContent(JsonSerializer.Serialize(requestBody, options), Encoding.UTF8, "application/json");
            request.Content = content;

            // Send the request
            HttpResponseMessage response = await _client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error: {response.StatusCode}");
                Console.WriteLine($"Error details: {await response.Content.ReadAsStringAsync()}");
                return null;
            }

            var responseData = await response.Content.ReadAsStringAsync();

            Console.WriteLine("Query successful. Parsing response data...");

            // Parse the response and return DataResult
            var dataResult = ParseResponseData(responseData);
            return dataResult;
        }

        private List<DataFrame> ParseResponseData(string responseData)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var root = JsonSerializer.Deserialize<Root>(responseData, options);

            if (root == null || root.Results == null)
            {
                Console.WriteLine("Failed to parse response data.");
                return null;
            }

            var result = root.Results["A"];

            if (result == null || result.Frames == null || result.Frames.Count == 0)
            {
                Console.WriteLine("No frames found in response data.");
                return null;
            }

            var dataFrames = new List<DataFrame>();

            foreach (var frame in result.Frames)
            {
                var fields = frame.Schema.Fields;
                var dataValues = frame.Data.Values;

                if (fields == null || dataValues == null)
                {
                    Console.WriteLine("Fields or Values are null.");
                    continue;
                }

                if (fields.Count != dataValues.Count)
                {
                    Console.WriteLine("Mismatch in number of fields and value arrays.");
                    continue;
                }

                // Updated to use List<IList>
                var values = new List<IList>();

                for (int i = 0; i < dataValues.Count; i++)
                {
                    var field = fields[i];

                    // Choose the type based on the field type
                    switch (field.Type)
                    {
                        case "time":
                            var timeList = JsonSerializer.Deserialize<List<long>>(dataValues[i].GetRawText());
                            var dateTimeList = new List<DateTime>();
                            foreach (var unixTime in timeList)
                            {
                                var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(unixTime).UtcDateTime;
                                dateTimeList.Add(dateTime);
                            }
                            field.CSharpType = typeof(DateTime);
                            values.Add(dateTimeList);
                            break;

                        case "number":
                            var numberList = JsonSerializer.Deserialize<List<double>>(dataValues[i].GetRawText());
                            field.CSharpType = typeof(double);
                            values.Add(numberList);
                            break;

                        // Add more cases if there are other types
                        default:
                            Console.WriteLine($"Unknown field type: {field.Type}");
                            values.Add(null);
                            break;
                    }
                }

                var dataResult = new DataFrame
                {
                    Fields = fields,
                    Values = values
                };

                dataFrames.Add(dataResult);
            }
            return dataFrames;
        }
    }
}
