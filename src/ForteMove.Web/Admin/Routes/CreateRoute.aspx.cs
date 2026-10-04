using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.UI.WebControls;
using ForteMove.Business.Services;
using ForteMove.Models.Common;
using ForteMove.Models.Routing;
using ForteMove.Web.Infrastructure;

namespace ForteMove.Web.Admin.Routes
{
    public partial class CreateRoute : AdminPage
    {
        private const string BuilderStateKey = "ForteMove.RouteBuilder";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                BuilderState = new RouteBuilderState();
                BindCreationOptions();
                BindDraftStops();
            }
        }

        protected void btnAddExistingStop_Click(object sender, EventArgs e)
        {
            ClearStopDraftErrors();
            long stopId;
            if (!long.TryParse(ddlExistingStop.SelectedValue, NumberStyles.None, CultureInfo.InvariantCulture, out stopId))
            {
                ShowStopDraftErrors(new[] { new ValidationError("ExistingStopId", "Select an active stop to add.") });
                return;
            }

            RouteCreationOptions options = ServiceFactory.CreateRouteService().GetCreationOptions();
            StopOption stop = options.ActiveStops.FirstOrDefault(item => item.StopId == stopId);
            if (stop == null)
            {
                ShowStopDraftErrors(new[] { new ValidationError("ExistingStopId", "The selected stop is no longer available.") });
                BindAvailableStops(options.ActiveStops);
                return;
            }

            RouteBuilderState state = BuilderState;
            if (state.Stops.Any(item => item.ExistingStopId == stopId))
            {
                ShowStopDraftErrors(new[] { new ValidationError("ExistingStopId", "This stop is already on the route.") });
                return;
            }

            state.Stops.Add(new RouteDraftStop
            {
                DraftKey = "existing-" + stop.StopId.ToString(CultureInfo.InvariantCulture),
                ExistingStopId = stop.StopId,
                StopCode = stop.StopCode,
                StopName = stop.StopName,
                Area = stop.Area
            });
            BuilderState = state;
            BindDraftStops();
            BindAvailableStops(options.ActiveStops);
        }

        protected void btnShowNewStop_Click(object sender, EventArgs e)
        {
            ClearStopDraftErrors();
            UpdateSuggestedStopCode();
            pnlNewStop.Visible = true;
            txtStopName.Focus();
        }

        protected void btnCancelNewStop_Click(object sender, EventArgs e)
        {
            ClearNewStopInputs();
            ClearStopDraftErrors();
            pnlNewStop.Visible = false;
        }

        protected void btnAddNewStop_Click(object sender, EventArgs e)
        {
            ClearStopDraftErrors();
            IList<ValidationError> errors = new List<ValidationError>();
            string stopName = (txtStopName.Text ?? string.Empty).Trim();
            string area = (txtArea.Text ?? string.Empty).Trim();
            if (stopName.Length == 0)
            {
                errors.Add(new ValidationError("StopName", "Stop name is required."));
            }

            if (area.Length == 0)
            {
                errors.Add(new ValidationError("Area", "Area is required."));
            }

            decimal? latitude = ParseDecimal(txtLatitude, "Latitude", "Latitude", errors);
            decimal? longitude = ParseDecimal(txtLongitude, "Longitude", "Longitude", errors);
            if (latitude.HasValue != longitude.HasValue)
            {
                errors.Add(new ValidationError(
                    latitude.HasValue ? "Longitude" : "Latitude",
                    "Enter both latitude and longitude, or leave both empty."));
            }

            if (errors.Count > 0)
            {
                ShowStopDraftErrors(errors);
                pnlNewStop.Visible = true;
                return;
            }

            RouteBuilderState state = BuilderState;
            state.Stops.Add(new RouteDraftStop
            {
                DraftKey = "new-" + Guid.NewGuid().ToString("N"),
                StopCode = litStopCode.Text,
                StopName = stopName,
                Area = area,
                Latitude = latitude,
                Longitude = longitude
            });
            BuilderState = state;
            ClearNewStopInputs();
            pnlNewStop.Visible = false;
            BindDraftStops();
            BindAvailableStops(ServiceFactory.CreateRouteService().GetCreationOptions().ActiveStops);
        }

        protected void rptRouteStops_ItemCommand(object source, RepeaterCommandEventArgs e)
        {
            string draftKey = Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture);
            RouteBuilderState state = BuilderState;
            int index = state.Stops.FindIndex(item => string.Equals(item.DraftKey, draftKey, StringComparison.Ordinal));
            if (index < 0)
            {
                return;
            }

            switch (e.CommandName)
            {
                case "MoveUp":
                    if (index > 0)
                    {
                        RouteDraftStop previous = state.Stops[index - 1];
                        state.Stops[index - 1] = state.Stops[index];
                        state.Stops[index] = previous;
                    }
                    break;
                case "MoveDown":
                    if (index < state.Stops.Count - 1)
                    {
                        RouteDraftStop next = state.Stops[index + 1];
                        state.Stops[index + 1] = state.Stops[index];
                        state.Stops[index] = next;
                    }
                    break;
                case "Remove":
                    state.Stops.RemoveAt(index);
                    break;
                default:
                    return;
            }

            BuilderState = state;
            ClearStopDraftErrors();
            BindDraftStops();
            BindAvailableStops(ServiceFactory.CreateRouteService().GetCreationOptions().ActiveStops);
        }

        protected void btnSaveRoute_Click(object sender, EventArgs e)
        {
            ClearRouteValidation();
            IList<ValidationError> bindingErrors = new List<ValidationError>();
            CreateRouteRequest request = BuildRequest(bindingErrors);
            if (bindingErrors.Count > 0)
            {
                ShowRouteErrors(bindingErrors);
                BindDraftStops();
                return;
            }

            ServiceResult<RouteCreationResult> result = ServiceFactory.CreateRouteService().CreateRoute(
                request,
                CurrentPrincipalContext.UserAccountId);
            if (!result.Succeeded)
            {
                ShowRouteErrors(result.Errors);
                BindDraftStops();
                RouteCreationOptions options = ServiceFactory.CreateRouteService().GetCreationOptions();
                litRouteCode.Text = Server.HtmlEncode(options.SuggestedRouteCode);
                BindAvailableStops(options.ActiveStops);
                return;
            }

            string message = string.Format(
                CultureInfo.CurrentCulture,
                "Route {0} was created successfully.",
                result.Value.RouteCode);
            FlashMessageStore.Put(Session, message, false);
            Response.Redirect(ResolveUrl("~/Admin/Routes/RouteList.aspx"), true);
        }

        private RouteBuilderState BuilderState
        {
            get
            {
                RouteBuilderState state = ViewState[BuilderStateKey] as RouteBuilderState;
                if (state == null)
                {
                    state = new RouteBuilderState();
                    ViewState[BuilderStateKey] = state;
                }

                return state;
            }
            set { ViewState[BuilderStateKey] = value; }
        }

        private void BindCreationOptions()
        {
            RouteCreationOptions options = ServiceFactory.CreateRouteService().GetCreationOptions();
            litRouteCode.Text = Server.HtmlEncode(options.SuggestedRouteCode);
            litStopCode.Text = Server.HtmlEncode(options.SuggestedStopCode);
            BindAvailableStops(options.ActiveStops);
        }

        private void BindAvailableStops(IEnumerable<StopOption> activeStops)
        {
            HashSet<long> selectedIds = new HashSet<long>(
                BuilderState.Stops
                    .Where(item => item.ExistingStopId.HasValue)
                    .Select(item => item.ExistingStopId.Value));
            IList<StopOption> available = (activeStops ?? Enumerable.Empty<StopOption>())
                .Where(item => !selectedIds.Contains(item.StopId))
                .OrderBy(item => item.StopName)
                .ThenBy(item => item.Area)
                .ToList();

            ddlExistingStop.Items.Clear();
            ddlExistingStop.Items.Add(new ListItem(
                available.Count == 0 ? "No active stops available" : "Select an existing stop",
                string.Empty));
            foreach (StopOption stop in available)
            {
                ddlExistingStop.Items.Add(new ListItem(
                    string.Format(CultureInfo.CurrentCulture, "{0} - {1} ({2})", stop.StopCode, stop.StopName, stop.Area),
                    stop.StopId.ToString(CultureInfo.InvariantCulture)));
            }

            ddlExistingStop.Enabled = available.Count > 0;
            btnAddExistingStop.Enabled = available.Count > 0;
        }

        private void BindDraftStops()
        {
            RouteBuilderState state = BuilderState;
            RefreshDraftStopCodes(state);
            var rows = state.Stops.Select((item, index) => new
            {
                item.DraftKey,
                item.StopCode,
                item.StopName,
                item.Area,
                item.IsNew,
                StopOrder = index + 1,
                CanMoveUp = index > 0,
                CanMoveDown = index < state.Stops.Count - 1
            }).ToList();

            rptRouteStops.DataSource = rows;
            rptRouteStops.DataBind();
            pnlNoStops.Visible = rows.Count == 0;
        }

        private void RefreshDraftStopCodes(RouteBuilderState state)
        {
            IList<RouteDraftStop> newStops = state.Stops.Where(item => item.IsNew).ToList();
            if (newStops.Count == 0)
            {
                return;
            }

            RouteService service = ServiceFactory.CreateRouteService();
            for (int index = 0; index < newStops.Count; index++)
            {
                newStops[index].StopCode = service.GetSuggestedStopCode(index);
            }

            BuilderState = state;
        }

        private void UpdateSuggestedStopCode()
        {
            int pendingNewStopCount = BuilderState.Stops.Count(item => item.IsNew);
            string code = ServiceFactory.CreateRouteService().GetSuggestedStopCode(pendingNewStopCount);
            litStopCode.Text = Server.HtmlEncode(code);
        }

        private CreateRouteRequest BuildRequest(IList<ValidationError> errors)
        {
            CreateRouteRequest request = new CreateRouteRequest
            {
                RouteName = txtRouteName.Text,
                EstimatedDistanceKm = ParseDecimal(txtDistance, "EstimatedDistanceKm", "Estimated distance", errors),
                EstimatedDurationMinutes = ParseInteger(txtDuration, "EstimatedDurationMinutes", "Estimated duration", errors),
                DefaultFare = ParseDecimal(txtFare, "DefaultFare", "Default fare", errors)
            };

            for (int index = 0; index < BuilderState.Stops.Count; index++)
            {
                RouteDraftStop draft = BuilderState.Stops[index];
                request.Stops.Add(new RouteStopRequest
                {
                    ExistingStopId = draft.ExistingStopId,
                    NewStop = draft.IsNew
                        ? new CreateStopRequest
                        {
                            StopName = draft.StopName,
                            Area = draft.Area,
                            Latitude = draft.Latitude,
                            Longitude = draft.Longitude
                        }
                        : null,
                    StopOrder = index + 1,
                    EstimatedMinutesFromOrigin = draft.EstimatedMinutesFromOrigin
                });
            }

            return request;
        }

        private void ShowRouteErrors(IEnumerable<ValidationError> errors)
        {
            IList<ValidationError> materialized = MaterializeErrors(errors, "Review the route details and try again.");
            foreach (ValidationError error in materialized)
            {
                WebControl control = GetRouteControlForField(error.Field);
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

        private void ShowStopDraftErrors(IEnumerable<ValidationError> errors)
        {
            IList<ValidationError> materialized = MaterializeErrors(errors, "Review the stop details and try again.");
            foreach (ValidationError error in materialized)
            {
                WebControl control = GetStopControlForField(error.Field);
                if (control != null)
                {
                    AddCssClass(control, "is-invalid");
                    control.Attributes["aria-invalid"] = "true";
                }
            }

            rptStopDraftErrors.DataSource = materialized;
            rptStopDraftErrors.DataBind();
            pnlStopDraftErrors.Visible = true;
            pnlStopDraftErrors.Focus();
        }

        private void ClearRouteValidation()
        {
            pnlErrors.Visible = false;
            foreach (WebControl control in new WebControl[] { txtRouteName, txtDistance, txtDuration, txtFare })
            {
                RemoveCssClass(control, "is-invalid");
                control.Attributes.Remove("aria-invalid");
            }
        }

        private void ClearStopDraftErrors()
        {
            pnlStopDraftErrors.Visible = false;
            foreach (WebControl control in new WebControl[] { ddlExistingStop, txtStopName, txtArea, txtLatitude, txtLongitude })
            {
                RemoveCssClass(control, "is-invalid");
                control.Attributes.Remove("aria-invalid");
            }
        }

        private void ClearNewStopInputs()
        {
            txtStopName.Text = string.Empty;
            txtArea.Text = string.Empty;
            txtLatitude.Text = string.Empty;
            txtLongitude.Text = string.Empty;
        }

        private WebControl GetRouteControlForField(string field)
        {
            switch (field ?? string.Empty)
            {
                case "RouteName": return txtRouteName;
                case "EstimatedDistanceKm": return txtDistance;
                case "EstimatedDurationMinutes": return txtDuration;
                case "DefaultFare": return txtFare;
                default: return null;
            }
        }

        private WebControl GetStopControlForField(string field)
        {
            switch (field ?? string.Empty)
            {
                case "ExistingStopId": return ddlExistingStop;
                case "StopName": return txtStopName;
                case "Area": return txtArea;
                case "Latitude": return txtLatitude;
                case "Longitude": return txtLongitude;
                default: return null;
            }
        }

        private static IList<ValidationError> MaterializeErrors(IEnumerable<ValidationError> errors, string fallback)
        {
            IList<ValidationError> materialized = errors == null
                ? new List<ValidationError>()
                : errors.Where(error => error != null).ToList();
            if (materialized.Count == 0)
            {
                materialized.Add(new ValidationError(string.Empty, fallback));
            }

            return materialized;
        }

        private static int? ParseInteger(TextBox input, string field, string label, IList<ValidationError> errors)
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

        private static decimal? ParseDecimal(TextBox input, string field, string label, IList<ValidationError> errors)
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
