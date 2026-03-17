namespace DynamicWebhookScheduling.Model
{
    public class Job
    {
        public required DateTime RunAt { get; set; }
        public required string WebhookUrl { get; set; }
        public TimeSpan Delay => RunAt.Subtract(DateTime.Now);
    }
}
