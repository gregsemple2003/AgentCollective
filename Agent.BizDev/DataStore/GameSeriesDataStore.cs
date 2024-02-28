using Agent.Core;
using Newtonsoft.Json;

namespace Agent.BizDev
{
    public class GameSeriesDataStore : RocksDbDataStore<GameSeries>
    {
        public GameSeriesDataStore(string path) : base(path, shouldWipe: false)
        {
        }

        protected override string GetKey(GameSeries series)
        {
            var key = $"{series.AppId}_{series.TimeGenerated.Year.ToString("D4")}_{series.TimeGenerated.Month.ToString("D2")}";
            return key;
        }

        public Task<List<GameSeries>> Load(int gameAppId, DateTime startTime, DateTime endTime)
        {
            var results = new List<GameSeries>();

            // Assuming each month's data is stored separately in RocksDb
            for (DateTime date = startTime; date <= endTime; date = date.AddMonths(1))
            {
                var key = $"{gameAppId}_{date.Year.ToString("D4")}_{date.Month.ToString("D2")}";
                if (_db.HasKey(key))
                {
                    var value = _db.Get(key);
                    var seriesList = JsonConvert.DeserializeObject<List<GameSeries>>(value);
                    results.AddRange(seriesList.Where(series => series.TimeGenerated >= startTime && series.TimeGenerated <= endTime));
                }
            }

            return Task.FromResult(results);
        }

        public void Add(GameSeries series)
        {
            var key = GetKey(series);

            if (_db.HasKey(key))
            {
                var existingValue = _db.Get(key); // Assuming this returns a string
                var existingSeriesList = JsonConvert.DeserializeObject<List<GameSeries>>(existingValue);

                // Remove all entries for the same day
                existingSeriesList.RemoveAll(gs => gs.TimeGenerated.Date == series.TimeGenerated.Date);

                // Add the new series entry
                existingSeriesList.Add(series);

                var updatedValue = JsonConvert.SerializeObject(existingSeriesList);
                _db.Put(key, updatedValue);
            }
            else
            {
                var serializedNewSeries = JsonConvert.SerializeObject(series);
                _db.Put(key, $"[{serializedNewSeries}]");
            }

            All.Add(series);
        }

    }
}
