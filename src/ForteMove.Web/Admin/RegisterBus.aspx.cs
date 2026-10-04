using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Business.Services;
using ForteMove.Models.Common;
using ForteMove.Models.Fleet;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Admin
{
    public partial class RegisterBus : AdminPage
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            txtManufactureYear.Attributes["max"] = (DateTime.Today.Year + 1)
                .ToString(CultureInfo.InvariantCulture);

            if (!IsPostBack)
            {
                BindRegistrationOptions();
            }
        }

        protected void btnRegisterBus_Click(object sender, EventArgs e)
        {
            ClearValidationState();

            IList<ValidationError> bindingErrors = new List<ValidationError>();
            RegisterBusRequest request = BuildRequest(bindingErrors);
            if (bindingErrors.Count > 0)
            {
                ShowErrors(bindingErrors);
                return;
            }

            BusService busService = ServiceFactory.CreateBusService();
            ServiceResult<BusRegistrationResult> result = busService.RegisterBus(
                request,
                CurrentPrincipalContext.UserAccountId);

            if (!result.Succeeded)
            {
                ShowErrors(result.Errors);
                return;
            }

            string message = string.Format(
                CultureInfo.CurrentCulture,
                "Bus {0} was registered successfully.",
                result.Value.FleetNumber);
            if (result.HasWarnings)
            {
                message = message + " " + string.Join(" ", result.Warnings);
            }

            FlashMessageStore.Put(Session, message, result.HasWarnings);
            Response.Redirect(ResolveUrl("~/Admin/FleetList.aspx"), true);
        }

        private void BindRegistrationOptions()
        {
            BusRegistrationOptions options = ServiceFactory
                .CreateBusService()
                .GetRegistrationOptions();

            BindLookup(ddlCategory, options.Categories, "Select a category");
            BindLookup(ddlPropulsion, options.PropulsionTypes, "Select propulsion");
            txtFleetNumber.Text = options.SuggestedFleetNumber;
        }

        private RegisterBusRequest BuildRequest(IList<ValidationError> errors)
        {
            return new RegisterBusRequest
            {
                FleetNumber = txtFleetNumber.Text,
                RegistrationNumber = txtRegistrationNumber.Text,
                Vin = txtVin.Text,
                Make = txtMake.Text,
                Model = txtModel.Text,
                ManufactureYear = ParseInteger(txtManufactureYear, "ManufactureYear", "Manufacture year", errors),
                BusCategoryId = ParseInteger(ddlCategory, "BusCategoryId", "Bus category", errors),
                PassengerCapacity = ParseInteger(txtPassengerCapacity, "PassengerCapacity", "Passenger capacity", errors),
                PropulsionTypeId = ParseInteger(ddlPropulsion, "PropulsionTypeId", "Propulsion type", errors),
                FuelTankCapacityLitres = ParseDecimal(txtFuelCapacity, "FuelTankCapacityLitres", "Fuel-tank capacity", errors),
                BatteryCapacityKwh = ParseDecimal(txtBatteryCapacity, "BatteryCapacityKwh", "Battery capacity", errors),
                OdometerKilometres = ParseDecimal(txtOdometer, "OdometerKilometres", "Odometer", errors),
                LicenceExpiryDate = ParseDate(txtLicenceExpiry, "LicenceExpiryDate", "Licence expiry date", errors),
                RoadworthyExpiryDate = ParseDate(txtRoadworthyExpiry, "RoadworthyExpiryDate", "Roadworthy expiry date", errors),
                InsuranceExpiryDate = ParseDate(txtInsuranceExpiry, "InsuranceExpiryDate", "Insurance expiry date", errors)
            };
        }

        private void ShowErrors(IEnumerable<ValidationError> errors)
        {
            IList<ValidationError> materialized = errors == null
                ? new List<ValidationError>()
                : errors.Where(error => error != null).ToList();

            if (materialized.Count == 0)
            {
                materialized.Add(new ValidationError(string.Empty, "Review the bus details and try again."));
            }

            foreach (ValidationError error in materialized)
            {
                WebControl control = GetControlForField(error.Field);
                if (control != null)
                {
                    AddCssClass(control, "is-invalid");
                    control.Attributes["aria-invalid"] = "true";
                }
            }

            rptErrors.DataSource = materialized;
            rptErrors.DataBind();
            pnlErrors.Visible = true;
            pnlErrors.Focus();
        }

        private void ClearValidationState()
        {
            pnlErrors.Visible = false;
            foreach (WebControl control in GetAllInputControls())
            {
                RemoveCssClass(control, "is-invalid");
                control.Attributes.Remove("aria-invalid");
            }
        }

        private WebControl GetControlForField(string field)
        {
            switch (field ?? string.Empty)
            {
                case "FleetNumber": return txtFleetNumber;
                case "RegistrationNumber": return txtRegistrationNumber;
                case "Vin": return txtVin;
                case "Make": return txtMake;
                case "Model": return txtModel;
                case "ManufactureYear": return txtManufactureYear;
                case "BusCategoryId": return ddlCategory;
                case "PassengerCapacity": return txtPassengerCapacity;
                case "PropulsionTypeId": return ddlPropulsion;
                case "FuelTankCapacityLitres": return txtFuelCapacity;
                case "BatteryCapacityKwh": return txtBatteryCapacity;
                case "OdometerKilometres": return txtOdometer;
                case "LicenceExpiryDate": return txtLicenceExpiry;
                case "RoadworthyExpiryDate": return txtRoadworthyExpiry;
                case "InsuranceExpiryDate": return txtInsuranceExpiry;
                default: return null;
            }
        }

        private IEnumerable<WebControl> GetAllInputControls()
        {
            return new WebControl[]
            {
                txtFleetNumber, txtRegistrationNumber, txtVin, txtMake, txtModel,
                txtManufactureYear, ddlCategory, txtPassengerCapacity, ddlPropulsion,
                txtFuelCapacity, txtBatteryCapacity, txtOdometer, txtLicenceExpiry,
                txtRoadworthyExpiry, txtInsuranceExpiry
            };
        }

        private static void BindLookup(
            DropDownList list,
            IEnumerable<LookupOption> options,
            string prompt)
        {
            list.DataSource = options ?? Enumerable.Empty<LookupOption>();
            list.DataTextField = "DisplayName";
            list.DataValueField = "Id";
            list.DataBind();
            list.Items.Insert(0, new ListItem(prompt, string.Empty));
        }

        private static int? ParseInteger(
            TextBox input,
            string field,
            string label,
            IList<ValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(input.Text))
            {
                return null;
            }

            int value;
            if (int.TryParse(input.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            errors.Add(new ValidationError(field, label + " must be a whole number."));
            return null;
        }

        private static int? ParseInteger(
            DropDownList input,
            string field,
            string label,
            IList<ValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(input.SelectedValue))
            {
                return null;
            }

            int value;
            if (int.TryParse(input.SelectedValue, NumberStyles.None, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            errors.Add(new ValidationError(field, "Select a valid " + label.ToLowerInvariant() + "."));
            return null;
        }

        private static decimal? ParseDecimal(
            TextBox input,
            string field,
            string label,
            IList<ValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(input.Text))
            {
                return null;
            }

            decimal value;
            if (decimal.TryParse(input.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            errors.Add(new ValidationError(field, label + " must be a valid number."));
            return null;
        }

        private static DateTime? ParseDate(
            TextBox input,
            string field,
            string label,
            IList<ValidationError> errors)
        {
            if (string.IsNullOrWhiteSpace(input.Text))
            {
                return null;
            }

            DateTime value;
            if (DateTime.TryParseExact(
                input.Text,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out value))
            {
                return value;
            }

            errors.Add(new ValidationError(field, label + " must be a valid date."));
            return null;
        }

        private static void AddCssClass(WebControl control, string cssClass)
        {
            IList<string> classes = (control.CssClass ?? string.Empty)
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            if (!classes.Contains(cssClass))
            {
                classes.Add(cssClass);
            }

            control.CssClass = string.Join(" ", classes);
        }

        private static void RemoveCssClass(WebControl control, string cssClass)
        {
            control.CssClass = string.Join(
                " ",
                (control.CssClass ?? string.Empty)
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(item => !string.Equals(item, cssClass, StringComparison.Ordinal)));
        }
    }
}
