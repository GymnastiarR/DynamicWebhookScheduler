using DynamicWebhookScheduling;
using System.Net;

public class JobWorker : BackgroundService
{
    private readonly JobService _jobService;
    private readonly HttpClient _httpClient;

    public JobWorker(JobService jobService, IHttpClientFactory clientFactory)
    {
        _jobService = jobService;
        this._httpClient = clientFactory.CreateClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _jobService.WaitForJobsAsync(stoppingToken);

            var nextJob = _jobService.PeekNextJob();
            if (nextJob == null) continue;

            var delay = nextJob.RunAt - DateTime.Now;

            if (delay > TimeSpan.Zero)
            {
                try
                {
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                        stoppingToken,
                        _jobService.GetInterruptToken()
                    );

                    await Task.Delay(delay, linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    continue;
                }
            }

            var jobToExecute = _jobService.GetNextJob(stoppingToken);

            if (jobToExecute != null)
            {
                this._jobService.RunJob(jobToExecute);
            }
        }
    }
}