using BizDevAgent.Model;
using Newtonsoft.Json;
using RocksDbSharp;
using System.Diagnostics;

namespace BizDevAgent.DataStore
{
    public class GameSeriesDataStore
    {
        /// <summary>
        /// For debugging purposes, use to clear the cache.
        /// </summary>
        public const bool ShouldWipeDatabase = false;

        /// <summary>
        /// All the series that have been loaded.
        /// </summary>
        public List<GameSeries> All;

        private RocksDb _db;

        public GameSeriesDataStore(string path)
        {
            var options = new DbOptions().SetCreateIfMissing(true);
            Directory.CreateDirectory(path);
            _db = RocksDb.Open(options, path);

            All = new List<GameSeries>();

            if (ShouldWipeDatabase)
            {
                using (var iterator = _db.NewIterator())
                {
                    iterator.SeekToFirst();
                    while (iterator.Valid())
                    {
                        _db.Remove(iterator.Key());
                        iterator.Next();
                    }
                }
            }
        }

        public async Task<List<GameSeries>> Load(int gameAppId, DateTime startTime, DateTime endTime)
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

            return results;
        }

        public void Add(GameSeries series)
        {
            var key = $"{series.AppId}_{series.TimeGenerated.Year.ToString("D4")}_{series.TimeGenerated.Month.ToString("D2")}";

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
