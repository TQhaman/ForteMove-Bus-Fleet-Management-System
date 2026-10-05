using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.UI;
using System.Web.UI.WebControls;
using ForteMove.Models.Common;
using ForteMove.Models.Maintenance;
namespace ForteMove.Web.Infrastructure
{
    public abstract class MaintenanceAdminPage:AdminPage
    {
        protected long Id { get { return QueryId("id"); } }
        protected long Actor { get { return CurrentPrincipalContext.UserAccountId; } }
        protected long QueryId(string name){long id;return long.TryParse(Request.QueryString[name],out id)&&id>0?id:0;}
        protected string Link(string page,long id){return ResolveUrl("~/Admin/Maintenance/"+page+".aspx?id="+id.ToString(CultureInfo.InvariantCulture));}
        protected string AssignmentLink(long id){return ResolveUrl("~/Admin/Assignments/AssignmentDetails.aspx?id="+id.ToString(CultureInfo.InvariantCulture));}
        protected long Selected(DropDownList c){long id;return long.TryParse(c.SelectedValue,out id)?id:0;}
        protected int? Integer(TextBox c){if(string.IsNullOrWhiteSpace(c.Text))return null;int v;if(!int.TryParse(c.Text,NumberStyles.None,CultureInfo.InvariantCulture,out v))throw new FormatException("Enter a valid whole number.");return v;}
        protected decimal? Number(TextBox c){if(string.IsNullOrWhiteSpace(c.Text))return null;decimal v;if(!decimal.TryParse(c.Text,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out v))throw new FormatException("Enter a valid number.");return v;}
        protected DateTime? DateInput(TextBox c){if(string.IsNullOrWhiteSpace(c.Text))return null;DateTime v;if(!DateTime.TryParseExact(c.Text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out v))throw new FormatException("Enter a valid date.");return v;}
        protected byte[] Token(string name){return ViewState[name] as byte[];}
        protected void Buses(DropDownList c,bool all=false)
        {
            c.Items.Clear();c.Items.Add(new ListItem(all?"All buses":"Select a bus",""));
            foreach(var b in ServiceFactory.CreateMaintenanceService().GetBuses())c.Items.Add(new ListItem(b.FleetNumber,b.BusId.ToString(CultureInfo.InvariantCulture)));
        }
        protected void EnumOptions<T>(DropDownList c,bool all=false){c.Items.Clear();if(all)c.Items.Add(new ListItem("All",""));foreach(var v in Enum.GetValues(typeof(T)))c.Items.Add(new ListItem(MaintenancePresentation.Label(v),v.ToString()));}
        protected void Choose(DropDownList c,object value){var item=c.Items.FindByValue(Convert.ToString(value,CultureInfo.InvariantCulture));if(item!=null){c.ClearSelection();item.Selected=true;}}
        protected void Message(string message,bool success=false)
        {
            var panel=FindControlRecursive(this,"pnlMessage") as Panel;var lit=FindControlRecursive(this,"litMessage") as Literal;
            if(panel!=null){panel.Visible=true;panel.CssClass=success?"alert alert-success":"alert alert-danger";}
            if(lit!=null)lit.Text=Server.HtmlEncode(message);
        }
        protected bool Result<T>(ServiceResult<T> r){if(r.Succeeded)return true;Message(string.Join(" ",r.Errors.Select(x=>x.Message)));return false;}
        protected void Saved(string page,long id,string message){FlashMessageStore.Put(Session,message,false);Response.Redirect(Link(page,id),false);Context.ApplicationInstance.CompleteRequest();}
        protected void BindFlash(){var flash=FlashMessageStore.Take(Session);if(flash!=null)Message(flash.Text,true);}
        private static Control FindControlRecursive(Control parent,string name){if(parent.ID==name)return parent;foreach(Control c in parent.Controls){var found=FindControlRecursive(c,name);if(found!=null)return found;}return null;}
    }
}
