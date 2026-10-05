using System;
using System.Globalization;
using ForteMove.Models.Passengers;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Passenger
{
    public partial class Wallet : PassengerPage
    {
        private decimal ReviewedAmount{get{return ViewState["Amount"]==null?0m:(decimal)ViewState["Amount"];}set{ViewState["Amount"]=value;}}
        private decimal ReviewedBefore{get{return ViewState["Before"]==null?0m:(decimal)ViewState["Before"];}set{ViewState["Before"]=value;}}
        private byte[] WalletRowVersion{get{return ViewState["WalletRv"] as byte[];}set{ViewState["WalletRv"]=value;}}
        private Guid OperationToken{get{return ViewState["Token"]==null?Guid.Empty:(Guid)ViewState["Token"];}set{ViewState["Token"]=value;}}
        private string PreviewFingerprint{get{return ViewState["Fingerprint"] as string;}set{ViewState["Fingerprint"]=value;}}

        protected void Page_Load(object sender,EventArgs e){if(!IsPostBack){OperationToken=Guid.NewGuid();BindWallet();}}

        protected void btnReview_Click(object sender,EventArgs e)
        {
            decimal amount;decimal? value=decimal.TryParse(txtAmount.Text,NumberStyles.Number,CultureInfo.CurrentCulture,out amount)?amount:(decimal?)null;
            var result=ServiceFactory.CreatePassengerService().PreviewTopUp(CurrentPrincipalContext.UserAccountId,value,OperationToken);
            if(!result.Succeeded){Show(string.Join(" ",System.Linq.Enumerable.Select(result.Errors,x=>x.Message)),true);pnlReview.Visible=false;return;}
            TopUpPreview preview=result.Value;ReviewedAmount=preview.Amount;ReviewedBefore=preview.BalanceBefore;WalletRowVersion=preview.WalletRowVersion;OperationToken=preview.OperationToken;PreviewFingerprint=preview.Fingerprint;
            litBefore.Text=preview.BalanceBefore.ToString("C",CultureInfo.CurrentCulture);litAmount.Text=preview.Amount.ToString("C",CultureInfo.CurrentCulture);litAfter.Text=preview.BalanceAfter.ToString("C",CultureInfo.CurrentCulture);pnlReview.Visible=true;
        }

        protected void btnConfirm_Click(object sender,EventArgs e)
        {
            var result=ServiceFactory.CreatePassengerService().TopUpWallet(CurrentPrincipalContext.UserAccountId,new TopUpRequest{Amount=ReviewedAmount,ReviewedBalanceBefore=ReviewedBefore,WalletRowVersion=WalletRowVersion,OperationToken=OperationToken,PreviewFingerprint=PreviewFingerprint,ClientIpAddress=ClientIpAddress.From(Request)});
            if(!result.Succeeded){Show(string.Join(" ",System.Linq.Enumerable.Select(result.Errors,x=>x.Message)),true);pnlReview.Visible=false;OperationToken=Guid.NewGuid();BindWallet();return;}
            Show("Top-up complete. Your wallet balance is now "+result.Value.BalanceAfter.ToString("C",CultureInfo.CurrentCulture)+".",false);pnlReview.Visible=false;txtAmount.Text=string.Empty;OperationToken=Guid.NewGuid();BindWallet();
        }

        protected void btnCancel_Click(object sender,EventArgs e){pnlReview.Visible=false;OperationToken=Guid.NewGuid();}

        private void BindWallet(){PassengerWalletDetails value=ServiceFactory.CreatePassengerService().GetWallet(CurrentPrincipalContext.UserAccountId,50);if(value==null){Response.Redirect(ResolveUrl("~/Errors/Unexpected.aspx"),true);return;}litBalance.Text=value.CurrentBalance.ToString("C",CultureInfo.CurrentCulture);rptTransactions.DataSource=value.RecentTransactions;rptTransactions.DataBind();pnlNoTransactions.Visible=value.RecentTransactions.Count==0;}
        private void Show(string message,bool error){pnlMessage.CssClass=error?"alert alert-danger":"alert alert-success";litMessage.Text=Server.HtmlEncode(message);pnlMessage.Visible=true;}
        protected string FormatTransaction(object value){return Convert.ToString(value).Replace("SimulatedTopUp","Wallet top-up").Replace("TicketPurchase","Ticket purchase").Replace("TripCancellationRefund","Trip cancellation refund");}
        protected string FormatOccurred(object value){return ServiceFactory.CreatePassengerService().ToOperationalTime((DateTime)value).ToString("g",CultureInfo.CurrentCulture);}
        protected string FormatAmount(object type,object amount){decimal value=(decimal)amount;bool debit=Convert.ToString(type)==WalletTransactionType.TicketPurchase.ToString();return (debit?"- ":"+ ")+value.ToString("C",CultureInfo.CurrentCulture);}
    }
}
