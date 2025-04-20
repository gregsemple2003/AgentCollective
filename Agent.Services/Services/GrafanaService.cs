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
                BaseAddress = new Uri("https://lastepoch.grafana.net")
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
			//Cookie: _ga=GA1.2.1098490339.1718732791; rl_page_init_referrer=RudderEncrypt%3AU2FsdGVkX1%2B%2Fj6QJVmgiBmaQTom6AjEkuOFE%2BBCb%2BCLpDFL0F99KiKwOLBBq0ju1; rl_page_init_referring_domain=RudderEncrypt%3AU2FsdGVkX1%2Bw%2FfPCZcI261PxMVR6wgMs6T71BlBbeYQKaZ4p8DtRQbHZt9DWSLQ0; intercom-id-agpb1wfw=e3735ada-2c9a-4d62-9633-131f0d70d36a; intercom-device-id-agpb1wfw=796fd49f-2855-42da-a467-a80dc1bb269c; mbox=PC#cecc54ffc2684c99b2e68838b7b9a82f.34_0#1801502041|session#05677f4cf3bb46aabeb58dd91b4f597e#1738259101; _gid=GA1.2.217479525.1744048518; grafana_session=1d70d00c023e70a45eb1463834edc29a; grafana_session_expiry=1744049122; rl_anonymous_id=RudderEncrypt%3AU2FsdGVkX19c75XuY19t7oV0nmI5uyaVSpsnNu4CgcyeeE1G56kW5ltWlxgbgjIb%2Bm9MAUcvBd7iimbYfXvsLg%3D%3D; rl_user_id=RudderEncrypt%3AU2FsdGVkX1%2BSFqUmE0g%2Bs90vzUMbk8dLGlKPgv%2FNnkVdB7brnvK4eKcZ7xBFvAWnt5Gz5obE8%2F9eEGa7bYf4Z4mqVMgRxvDi7Dv%2F9o8yCvA%3D; rl_trait=RudderEncrypt%3AU2FsdGVkX1%2B6XF9Y%2BY7lmnxUly%2FaMGFGMjOwlR50Mck4hAGoJNoi8PP%2BCUr4JsQHg7gx2TF%2B%2B%2FWQOpz5t8B7jW62LvvWi0ymrDGDm3NjxMHqjeoVovPDgdC4l3nWxC%2F%2BNUfWMMAFivSHsNL0sagViAD8HW3jysnUiZnR33NqnyUo%2BzlYs6tCH%2BvsNhhZowV8; _ga_Y0HRZEVBCW=GS1.2.1744048518.115.1.1744048529.49.0.0; rl_session=RudderEncrypt%3AU2FsdGVkX1%2FfnWHDAhAL5A9D%2Bxt0GXDBIdlY2L7iulOLgNhJqXiL7OTSZxWXmSKSvEQlPucCH0tfxoLvtOAnHUJumAO5jw0o%2FLZG9V3Znx135LseUf%2BS89X6ym6m9SYxWsr933VjhvX40ofJBn2BDQ%3D%3D; fs_lua=1.1744048530844; fs_uid=#o-1CN5TD-na1#67813331-63ad-4f9f-86f2-99fce065aed9:96b696f3-d1f5-4154-84b7-7ae0cdbfc1bb:1744048530844::1#7b3d1d46#/1775584534; intercom-session-agpb1wfw=VEQwVnlsOGo2ZjJEaVlFVjFuZ2RWK0MrNXkrY1R2N0lYWmdkd3cza1hMaUhkakZFOHFFUHNQTzhrVWsrMGxBWldEQk10REdoMnBMbUhVcFk2NjBCZTAwdi83RGptSGhJcUsrYitlZmZKTkU9LS1EZFVJc1JIUlFSZUlremFBQjVTNkNRPT0=--dabb28183d73377414e6fed07a31760322ef4328

			request.Headers.Add("Cookie", "_ga=GA1.2.1098490339.1718732791; rl_page_init_referrer=RudderEncrypt%3AU2FsdGVkX1%2B%2Fj6QJVmgiBmaQTom6AjEkuOFE%2BBCb%2BCLpDFL0F99KiKwOLBBq0ju1; rl_page_init_referring_domain=RudderEncrypt%3AU2FsdGVkX1%2Bw%2FfPCZcI261PxMVR6wgMs6T71BlBbeYQKaZ4p8DtRQbHZt9DWSLQ0; intercom-id-agpb1wfw=e3735ada-2c9a-4d62-9633-131f0d70d36a; intercom-device-id-agpb1wfw=796fd49f-2855-42da-a467-a80dc1bb269c; _gid=GA1.2.217479525.1744048518; rl_group_id=RudderEncrypt%3AU2FsdGVkX19FFeylkOVJiaG%2B1sMYH3TcccXGj99uqUk%3D; rl_group_trait=RudderEncrypt%3AU2FsdGVkX1%2B8Rv4r0F45Yie8b8vj5cDNY9eXcmqXgkQ%3D; rl_anonymous_id=RudderEncrypt%3AU2FsdGVkX1%2B1MyhGizor5UPNcqtJhG95EnlWvF7wLEffcubyf3OibLMTHo7D%2FsQWRGn8XV0QHgV9DSmY%2FO7BBg%3D%3D; rl_user_id=RudderEncrypt%3AU2FsdGVkX18lFkrBJoaLXukKX6UvsgeNRZZWDRa6BSkDcXSvUo3xDdX%2BSmBeMARnTtc%2F2D8YjIyGAQIDOHyoanDwb1SGabkhmBEVoddJ%2BTk%3D; rl_trait=RudderEncrypt%3AU2FsdGVkX18MEm4p4k9Psgao4B9laOKKu1i2tLXqGIKELlQ1NeJiahJMBXrSjsR8ai4Napl1d4pjZ%2F6UmQZg%2FMMjODZWEX5YROSK4NdRhBae%2FzNf3jfCwOIiJYZr0xiRHL26dlYmd2di0%2FsRUj4t1%2BO31Jwall0LnNiA1a4TqR2yLZs6GtqRPKFwsyVry5G6; at_check=true; mbox=PC#cecc54ffc2684c99b2e68838b7b9a82f.34_0#1807296119|session#9478025be04b4ccb9bc9eaafe997945e#1744053179; fs_uid=#o-1CN5TD-na1#67813331-63ad-4f9f-86f2-99fce065aed9:96b696f3-d1f5-4154-84b7-7ae0cdbfc1bb:1744048530844::8#7b3d1d46#/1775584555; intercom-session-agpb1wfw=QUJGL2ZRbFpTejJxQitRbVlOM3JzQmhBdHpnanZHRkJJS2hpS1pDT2Z3cW9YOGVlakNiV1o0dWVUSE1hb2NmL3hKQmRRbFF4b0dZQmVuR2pUVmNKejc2WGtwTmtqUG1TRzM2NDdjRWl3YXM9LS0yTmRXSjZ3Ym5VYXdOS21wV3JNSVV3PT0=--1d48055b3ddc647fc0ae33a4940ec4ffcc4dab6e; _ga_Y0HRZEVBCW=GS1.2.1744050744.116.1.1744051462.2.0.0; rl_session=RudderEncrypt%3AU2FsdGVkX19fFi9OzPjR8Qae5WiCLGWcrw9VOCsMrNKpAO7Q6TVLSpwBkW3F0bJgmN1Qv8THG4pmiDYvkgjMW0nyxmqvEBBACMvMkUrMqiR7RkM8lcdJJFwcxA8%2BpovjE6Th22eiNiT1rkdgabvoqQ%3D%3D; fs_lua=1.1744051483840; grafana_session=c82f634e0b0f97ef7624219918c264ad; grafana_session_expiry=1744052602");

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
