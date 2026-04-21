using DynamicWebhookScheduling.Model;

namespace DynamicWebhookScheduling.Applications.Repositories
{
    public interface IJobRepository
    {
        Task SaveJob(Job job);

        Task DeleteJob(string jobId);
    }
}
