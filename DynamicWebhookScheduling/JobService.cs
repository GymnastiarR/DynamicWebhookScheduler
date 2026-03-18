using DynamicWebhookScheduling.Applications.Services;
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
        private readonly PersistanceService _persistanceService;
        private readonly HttpClient _httpClient;

        public JobService(PersistanceService persistanceService, IHttpClientFactory httpClientFactory)
        {
            this._persistanceService = persistanceService;
            this._httpClient = httpClientFactory.CreateClient();
        }

        public void CreateJob(CreateDateTimeJobRequest data)
        {
            var request = new Request
            {
                Method = data.RequestDTO.Method,
                Url = data.RequestDTO.Url,
                Payload = data.RequestDTO.Payload,
                Queries = data.RequestDTO.Queries
            };

            var job = new Job
            {
                Request = request,
                RunAt = data.RunAt
            };

            this.InsertJob(job);

            Task.Run(() => this._persistanceService.SaveJob(job, job.CancellationTokenSource.Token));
        }

        public void CreateJob(CreateDelayJobDTO data)
        {
            var request = new Request
            {
                Method = data.RequestDTO.Method,
                Url = data.RequestDTO.Url,
                Payload = data.RequestDTO.Payload,
                Queries = data.RequestDTO.Queries
            };

            var job = new Job
            {
                Request = request,
                RunAt = RecalculateRunAt(data.RunAfter, data.Timestamp)
            };

            this.InsertJob(job);

            Task.Run(() => this._persistanceService.SaveJob(job, job.CancellationTokenSource.Token));
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

        public async Task RunJob(Job job)
        {
            //await this._httpClient.SendAsync();
            if (job.Id != null)
                job.CancellationTokenSource.Cancel();
            else
                this.RemoveJob(job);
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

                if (isInsertedAtFirst)
                {
                    _interruptTokenSource.Cancel();
                    _interruptTokenSource = new CancellationTokenSource();
                }
            }

            _signal.Release();
        }

        private void RemoveJob(Job job)
        {
            lock (_lock)
            {
                jobs.Remove(job);
                Task.Run(() => this._persistanceService.DeleteJob(job));
            }
        }
    }
}
