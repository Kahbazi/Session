using Microsoft.AspNetCore.Builder;

namespace Kahbazi.AspNetCore.Session
{
    public static class RoutingEndpointConventionBuilderExtensions
    {
        public static TBuilder SetSessionBehvior<TBuilder>(this TBuilder builder, SessionState sessionState) where TBuilder : IEndpointConventionBuilder
        {
            return builder.WithMetadata(new SessionStateAttribute(sessionState));
        }
    }
}
