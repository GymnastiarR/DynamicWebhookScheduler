namespace DynamicWebhookScheduling.Applications.Services
{
    public class WebhookService
    {
        public static Dictionary<Enums.RequestMethod, HttpMethod> Webhooks { get; set; } = new()
        {
            { Enums.RequestMethod.GET, HttpMethod.Get },
            { Enums.RequestMethod.POST, HttpMethod.Post },
            { Enums.RequestMethod.PUT, HttpMethod.Put },
            { Enums.RequestMethod.DELETE, HttpMethod.Delete },
        };

        public WebhookService()
        {
            
        }

        public Task SendRequest()
        {
            throw new NotImplementedException();
        }
    }
}
