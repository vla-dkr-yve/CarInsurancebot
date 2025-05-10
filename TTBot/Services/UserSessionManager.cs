using TTBot.Session;

namespace TTBot.Services
{
    public static class UserSessionManager
    {
        private static readonly Dictionary<long, UserSession> _sessions = new();

        public static UserSession GetOrCreateSession(long chatId)
        {
            if (!_sessions.TryGetValue(chatId, out var session))
            {
                session = new UserSession();
                _sessions[chatId] = session;
            }
            return session;
        }
    }
}
