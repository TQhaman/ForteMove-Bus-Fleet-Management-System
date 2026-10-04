using System.Web.SessionState;

namespace ForteMove.Web.Infrastructure
{
    public sealed class FlashMessage
    {
        public string Text { get; set; }

        public bool IsWarning { get; set; }
    }

    public static class FlashMessageStore
    {
        private const string SessionKey = "ForteMove.FlashMessage";

        public static void Put(HttpSessionState session, string text, bool isWarning)
        {
            if (session == null)
            {
                return;
            }

            session[SessionKey] = new FlashMessage
            {
                Text = text,
                IsWarning = isWarning
            };
        }

        public static FlashMessage Take(HttpSessionState session)
        {
            if (session == null)
            {
                return null;
            }

            FlashMessage message = session[SessionKey] as FlashMessage;
            session.Remove(SessionKey);
            return message;
        }
    }
}
