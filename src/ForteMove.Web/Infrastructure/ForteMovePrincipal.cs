using System;
using System.Security.Principal;
using ForteMove.Models.Security;

namespace ForteMove.Web.Infrastructure
{
    public sealed class ForteMovePrincipal : IPrincipal
    {
        private readonly ForteMoveIdentity identity;

        public ForteMovePrincipal(PrincipalContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            Context = context;
            identity = new ForteMoveIdentity(context);
        }

        public PrincipalContext Context { get; private set; }

        public IIdentity Identity
        {
            get { return identity; }
        }

        public bool IsInRole(string role)
        {
            return Context.IsActive &&
                Context.IsRoleActive &&
                !string.IsNullOrWhiteSpace(role) &&
                string.Equals(Context.Role.ToString(), role, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class ForteMoveIdentity : IIdentity
        {
            private readonly PrincipalContext context;

            public ForteMoveIdentity(PrincipalContext context)
            {
                this.context = context;
            }

            public string AuthenticationType
            {
                get { return "Forms"; }
            }

            public bool IsAuthenticated
            {
                get { return context.IsActive && context.IsRoleActive; }
            }

            public string Name
            {
                get { return context.Email ?? string.Empty; }
            }
        }
    }
}
