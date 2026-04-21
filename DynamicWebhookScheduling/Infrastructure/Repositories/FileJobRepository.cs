using DynamicWebhookScheduling.Applications.Repositories;
using DynamicWebhookScheduling.Applications.Services;
using DynamicWebhookScheduling.Model;
using System.Collections.Concurrent;
using System.Text.Json;

namespace DynamicWebhookScheduling.Infrastructure.Repositories
{
    public class FileJobRepository(FileService fileService) : IJobRepository
    {
        private readonly FileService _fileService = fileService;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> IdCtsPairs = [];
        private readonly ConcurrentDictionary<string, int> IdPointerPairs = [];
        private static readonly Lock @lock = new();

        public async Task SaveJob(Job job)
        {
            var cts = new CancellationTokenSource();

            this.IdCtsPairs.TryAdd(job.Id, cts);
            var jsonJob = JsonSerializer.Serialize(job);
            var pointer = await this._fileService.SaveData(jsonJob, cts.Token);
            if (pointer > 0)
                this.IdPointerPairs.TryAdd(job.Id, pointer);
        }

        public async Task DeleteJob(string jobId)
        {
            if (IdPointerPairs.TryGetValue(jobId, out var pointer))
            {
                await this._fileService.DeleteData(pointer);
                this.IdCtsPairs.TryRemove(jobId, out var _);
                this.IdPointerPairs.TryRemove(jobId, out var _);
                return;
            }

            if (this.IdCtsPairs.TryRemove(jobId, out var cts))
                cts.Cancel();
        }

        public async Task<List<Job>> LoadJobs()
        {
            var jobs = await this._fileService.LoadDatas();
            return jobs;
        }
    }
}
