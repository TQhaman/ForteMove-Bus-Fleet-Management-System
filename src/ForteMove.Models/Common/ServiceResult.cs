using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ForteMove.Models.Common
{
    public sealed class ServiceResult<T>
    {
        private ServiceResult(
            bool succeeded,
            T value,
            IEnumerable<ValidationError> errors,
            IEnumerable<string> warnings)
        {
            Succeeded = succeeded;
            Value = value;
            Errors = ToReadOnly(errors);
            Warnings = ToReadOnly(warnings);
        }

        public bool Succeeded { get; private set; }

        public T Value { get; private set; }

        public ReadOnlyCollection<ValidationError> Errors { get; private set; }

        public ReadOnlyCollection<string> Warnings { get; private set; }

        public bool HasWarnings
        {
            get { return Warnings.Count > 0; }
        }

        public static ServiceResult<T> Success(T value)
        {
            return new ServiceResult<T>(true, value, null, null);
        }

        public static ServiceResult<T> Success(T value, IEnumerable<string> warnings)
        {
            return new ServiceResult<T>(true, value, null, warnings);
        }

        public static ServiceResult<T> Failure(IEnumerable<ValidationError> errors)
        {
            if (errors == null)
            {
                throw new ArgumentNullException("errors");
            }

            ValidationError[] materializedErrors = errors.Where(error => error != null).ToArray();
            if (materializedErrors.Length == 0)
            {
                throw new ArgumentException("At least one validation error is required.", "errors");
            }

            return new ServiceResult<T>(false, default(T), materializedErrors, null);
        }

        public static ServiceResult<T> Failure(string field, string message)
        {
            return Failure(new[] { new ValidationError(field, message) });
        }

        private static ReadOnlyCollection<TItem> ToReadOnly<TItem>(IEnumerable<TItem> items)
        {
            IList<TItem> list = items == null
                ? new List<TItem>()
                : new List<TItem>(items);

            return new ReadOnlyCollection<TItem>(list);
        }
    }
}
