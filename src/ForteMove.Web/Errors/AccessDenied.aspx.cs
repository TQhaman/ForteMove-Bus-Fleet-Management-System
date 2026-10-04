using System;
using System.Web.UI;

namespace ForteMove.Web.Errors
{
    public partial class AccessDenied : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.StatusCode = 403;
            Response.TrySkipIisCustomErrors = true;
        }
    }
}
