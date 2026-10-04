using System;
using System.Web.UI;

namespace ForteMove.Web.Errors
{
    public partial class Unexpected : Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            Response.StatusCode = 500;
            Response.TrySkipIisCustomErrors = true;
        }
    }
}
