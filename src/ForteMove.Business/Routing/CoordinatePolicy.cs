using System;
using System.Collections.Generic;
using ForteMove.Models.Common;

namespace ForteMove.Business.Routing
{
    public static class CoordinatePolicy
    {
        public static IList<ValidationError> Validate(decimal? latitude,decimal? longitude,string field="")
        {
            var errors=new List<ValidationError>();
            if(latitude.HasValue!=longitude.HasValue)errors.Add(new ValidationError(field,"Supply both latitude and longitude, or leave both empty."));
            if(latitude.HasValue && (latitude.Value < -90 || latitude.Value > 90))errors.Add(new ValidationError(field,"Latitude must be between -90 and 90."));
            if(longitude.HasValue && (longitude.Value < -180 || longitude.Value > 180))errors.Add(new ValidationError(field,"Longitude must be between -180 and 180."));
            if(latitude.HasValue && decimal.Round(latitude.Value,6)!=latitude.Value)errors.Add(new ValidationError(field,"Latitude may use no more than six decimal places."));
            if(longitude.HasValue && decimal.Round(longitude.Value,6)!=longitude.Value)errors.Add(new ValidationError(field,"Longitude may use no more than six decimal places."));
            return errors;
        }
    }
}
