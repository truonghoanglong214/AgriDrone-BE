using System;
using System.Collections.Generic;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AgriDrone.SharedKernel.Application
{
    public class Result
    {
        protected Result(bool isSuccess, AppError error)
        {
            ArgumentNullException.ThrowIfNull(error);

            if (isSuccess && error != AppError.None)
            {
                throw new ArgumentException(
                    "A successful result cannot contain an error.",
                    nameof(error));
            }

            if (!isSuccess && error == AppError.None)
            {
                throw new ArgumentException(
                    "A failed result must contain an error.",
                    nameof(error));
            }

            IsSuccess = isSuccess;
            Error = error;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public AppError Error { get; }

        public static Result Success() =>
            new(true, AppError.None);

        public static Result Failure(AppError error) =>
            new(false, error);

        public static Result<T> Success<T>(T value) =>
            new(value, true, AppError.None);

        public static Result<T> Failure<T>(AppError error) =>
            new(default, false, error);
    }
}
