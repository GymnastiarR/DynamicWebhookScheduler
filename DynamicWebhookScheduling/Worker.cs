using DynamicWebhookScheduling;

public class JobWorker : BackgroundService
{
    private readonly JobService _jobService;

    public JobWorker(JobService jobService)
    {
        _jobService = jobService;
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

            var jobToExecute = _jobService.GetNextJob();

            if (jobToExecute != null)
            {
                Console.WriteLine($"Mengeksekusi Webhook: {jobToExecute.WebhookUrl}");
            }
        }
    }
}