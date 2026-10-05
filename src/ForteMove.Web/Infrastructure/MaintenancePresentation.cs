using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using ForteMove.Business.Time;
namespace ForteMove.Web.Infrastructure
{
    public static class MaintenancePresentation
    {
        public static string Label(object value)
        {
            string s=value==null?"":value.ToString();
            switch(s){case "NotDue":return "Not due";case "InProgress":return "In progress";case "UnderMaintenance":return "Under maintenance";case "OutOfService":return "Out of service";case "ServiceDate":return "Service date";case "ServiceOdometer":return "Service odometer";case "ComplianceExpiry":return "Compliance expiry";default:return s;}
        }
        public static string Date(DateTime? value){return value.HasValue?value.Value.ToString("dd MMM yyyy",CultureInfo.InvariantCulture):"Not recorded";}
        public static string Time(DateTime? utc){return utc.HasValue?new SystemClock().ToOperationalTime(utc.Value).ToString("dd MMM yyyy HH:mm",CultureInfo.InvariantCulture):"Not recorded";}
        public static string Odo(decimal? value){return value.HasValue?value.Value.ToString("N1",CultureInfo.GetCultureInfo("en-ZA"))+" km":"Not recorded";}
        public static string Money(decimal? value){return value.HasValue?value.Value.ToString("C",CultureInfo.GetCultureInfo("en-ZA")):"Not recorded";}
        public static string Cell(object text,string url=null)
        {
            var label=HttpUtility.HtmlEncode(Label(text));
            return url==null?label:"<a href=\""+HttpUtility.HtmlAttributeEncode(url)+"\">"+label+"</a>";
        }
        public static string Table(string[] headings,IEnumerable<string[]> rows,string empty)
        {
            var data=rows.ToList();if(data.Count==0)return "<p class=\"empty-state\">"+HttpUtility.HtmlEncode(empty)+"</p>";
            var s=new StringBuilder("<div class=\"table-responsive\"><table class=\"table align-middle\"><thead><tr>");
            foreach(var h in headings)s.Append("<th scope=\"col\">").Append(HttpUtility.HtmlEncode(h)).Append("</th>");
            s.Append("</tr></thead><tbody>");
            foreach(var row in data){s.Append("<tr>");foreach(var cell in row)s.Append("<td>").Append(cell).Append("</td>");s.Append("</tr>");}
            return s.Append("</tbody></table></div>").ToString();
        }
    }
}
