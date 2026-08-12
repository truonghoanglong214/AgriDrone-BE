using System;
using System.Collections.Generic;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AgriDrone.SharedKernel.Application
{
    public sealed class Result<T> : Result
    {
        private readonly T? _value;

        internal Result(
            T? value,
            bool isSuccess,
            AppError error)
            : base(isSuccess, error)
        {
            _value = value;
        }

        public T Value =>
            IsSuccess
                ? _value!
                : throw new InvalidOperationException(
                    "Cannot access the value of a failed result.");
    }
}
