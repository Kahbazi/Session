namespace Kahbazi.AspNetCore.Session
{
    public class SessionStateAttribute : ISessionStateMetadata
    {
        public SessionStateAttribute(SessionState state)
        {
            State = state;
        }

        public SessionState State { get; }
    }
}
