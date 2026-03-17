using DynamicWebhookScheduling.Controllers.DTO;
using DynamicWebhookScheduling.Model;

namespace DynamicWebhookScheduling
{
    public interface IJobService
    {
        void CreateJob(CreateDateTimeJobRequest data);
        void CreateJob(CreateDelayJobDTO data);
        Task<Job?> GetNextJob(CancellationToken cancellationToken = default);
    }
}