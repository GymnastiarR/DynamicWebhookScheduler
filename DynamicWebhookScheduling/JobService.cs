using DynamicWebhookScheduling.Controllers.DTO;
using DynamicWebhookScheduling.Model;
using System.Threading.Channels;

namespace DynamicWebhookScheduling
{
    public class JobService
    {
        private readonly SemaphoreSlim _signal = new(0);
        private readonly LinkedList<Job> jobs = new();
        private readonly System.Threading.Lock _lock = new();
        private CancellationTokenSource _interruptTokenSource = new();

        public JobService()
        {
            using(FileStream fs = new("jobs.json", FileMode.OpenOrCreate))
            {
                using StreamReader sr = new(fs);
                var content = sr.ReadToEnd();
                if (!string.IsNullOrEmpty(content))
                {
                    var deserializedJobs = System.Text.Json.JsonSerializer.Deserialize<List<Job>>(content);
                    if (deserializedJobs != null)
                    {
                        foreach (var job in deserializedJobs)
                        {
                            InsertJob(job);
                        }
                    }
                }
            }
        }

        public async void CreateJob(CreateDateTimeJobRequest data)
        {
            var job = new Job
            {
                WebhookUrl = data.WebhookUrl,
                RunAt = data.RunAt
            };

            this.InsertJob(job);
        }

        public async void CreateJob(CreateDelayJobDTO data)
        {
            var job = new Job
            {
                WebhookUrl = data.WebhookUrl,
                RunAt = RecalculateRunAt(data.RunAfter, data.Timestamp)
            };

            this.InsertJob(job);
        }

        public async Task WaitForJobsAsync(CancellationToken cancellationToken = default)
        {
            await _signal.WaitAsync(cancellationToken);
        }

        public Job? GetNextJob(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (jobs.Count == 0) return null;
                var job = jobs.First!.Value;
                jobs.Remove(job);
                return job;
            }
        }

        public Job? PeekNextJob(CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                if (jobs.Count == 0) return null;
                var job = jobs.First!.Value;
                return job;
            }
        }

        public CancellationToken GetInterruptToken() => _interruptTokenSource.Token;

        private static DateTime RecalculateRunAt(TimeSpan delay, DateTime timestamp)
        {
            var elapsed = DateTime.Now - timestamp;
            var remaining = delay - elapsed;
            return DateTime.Now.Add(remaining);
        }


        private void InsertJob(Job job)
        {
            bool isInsertedAtFirst = false;

            lock (_lock)
            {
                var currJob = jobs.First;
                while (currJob != null && currJob.Value.RunAt < job.RunAt)
                {
                    currJob = currJob.Next;
                }

                if (currJob == null)
                {
                    jobs.AddLast(job);
                    if (jobs.Count == 1) isInsertedAtFirst = true;
                }
                else
                {
                    var newNode = jobs.AddBefore(currJob, job);
                    if (newNode == jobs.First) isInsertedAtFirst = true;
                }
            }

            if (isInsertedAtFirst)
            {
                _interruptTokenSource.Cancel();
                _interruptTokenSource = new CancellationTokenSource();
            }

            _signal.Release();
        }
    }
}
