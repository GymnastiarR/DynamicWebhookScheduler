using System.ComponentModel.DataAnnotations;

namespace DynamicWebhookScheduling.Controllers.DTO
{
    public class CreateDelayJobRequest {
        [Required]
        public required RequestDTO RequestDTO { get; set; }
        
        [Required]
        public required int RunAfter { get; set; }

        [Required]
        public required DateTime Timestamp { get; set; }
    }
}
