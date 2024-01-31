﻿using BizDevAgent.DataStore;
using BizDevAgent.Model;
using BizDevAgent.Utilities;
using System.Web;
using Microsoft.Extensions.Configuration;

namespace BizDevAgent.Jobs
{
    public class JobRunner
    {
        private bool _isRunning = true;

        private readonly JobDataStore _jobDataStore;
        private readonly IConfiguration _configuration;

        public JobRunner(JobDataStore jobDataStore, IConfiguration configuration)
        {
            _jobDataStore = jobDataStore;
            _configuration = configuration;
        }

        public void AddJob(Job job)
        {
            _jobDataStore.All.Add(job);
        }

        public async Task Start()
        {
            while (_isRunning)
            {
                var nextJob = _jobDataStore.All.Where(job => job.Enabled)
                                    .OrderBy(job => job.ScheduledRunTime)
                                    .FirstOrDefault();
                if (nextJob != null && DateTime.Now > nextJob.ScheduledRunTime)
                {
                    var originalOut = Console.Out;
                    var originalError = Console.Error;
                    var runId = Guid.NewGuid();

                    using (var writerOut = new DualTextWriter(Console.Out))
                    using (var writerError = new DualTextWriter(Console.Error))
                    {
                        try
                        {
                            Console.SetOut(writerOut);
                            Console.SetError(writerError);

                            await nextJob.UpdateScheduledRunTime();
                            await nextJob.Run();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Job runner caught exception: {ex}");
                        }
                        finally
                        {
                            // Reset the original console streams
                            Console.SetOut(originalOut);
                            Console.SetError(originalError);

                            // Access captured output
                            string output = writerOut.CapturedOutput;
                            string error = writerError.CapturedOutput;

                            nextJob.LastRunTime = DateTime.Now;

                            // Next scheduled time shouldn't be in the past
                            nextJob.ScheduledRunTime = new DateTime(Math.Max(nextJob.ScheduledRunTime.Ticks, DateTime.Now.Ticks));

                            // Persist changes in job state so we don't redo old work
                            await _jobDataStore.SaveAll();

                            // Save output to logfile
                            string encodedJobName = HttpUtility.UrlEncode(nextJob.Name); // Encoding the job name to make it file-system safe
                            string logFileName = $"{encodedJobName}_{DateTime.Now:yyyy-MM-dd_HH-mm}_{runId}.log";
                            string logFilePath = Path.Combine(Paths.GetLogPath(), logFileName); // Replace with the actual directory path where logs should be stored
                            string combinedLogContent = $"OUTPUT:\n{output}\n\nERROR:\n{error}";
                            Directory.CreateDirectory(Paths.GetLogPath());
                            await File.WriteAllTextAsync(logFilePath, combinedLogContent);

                            // Send an email with the results
                            if (_configuration.GetValue<bool>("EmailJobLogs"))
                            {
                                await EmailUtils.SendEmail("gregsemple2003@gmail.com", $"Job Run - '{nextJob.Name}'", "", attachmentFilePath: logFilePath);
                            }
                        }
                    }
                }

                await Task.Delay(1000); // Wait for a second before checking for the next job
            }
        }

        public void Stop()
        {
            _isRunning = false;
        }
    }
}
