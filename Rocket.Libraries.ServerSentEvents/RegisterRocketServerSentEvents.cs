using Microsoft.Extensions.DependencyInjection;

namespace Rocket.Libraries.ServerSentEvents
{
    public static class RocketServerSentEventsRegistration
    {
        public static IServiceCollection SetupRocketServerSentEventsSingleton (this IServiceCollection services)
        {
            services.AddSingleton<IEventQueue, EventQueue> ();
            return services;
        }
    }
}