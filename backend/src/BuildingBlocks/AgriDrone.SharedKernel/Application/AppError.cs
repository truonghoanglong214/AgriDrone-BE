using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.SharedKernel.Application
{
    public sealed record AppError(
    string Code,
    string Description,
    ErrorType Type)
    {
        public static readonly AppError None =
            new(string.Empty, string.Empty, ErrorType.Failure);

        public static AppError NotFound(string code, string description) =>
            new(code, description, ErrorType.NotFound);

        public static AppError Conflict(string code, string description) =>
            new(code, description, ErrorType.Conflict);

        public static AppError Forbidden(string code, string description) =>
            new(code, description, ErrorType.Forbidden);

        public static AppError Unauthorized(string code, string description) =>
            new(code, description, ErrorType.Unauthorized);

        public static AppError Failure(string code, string description) =>
            new(code, description, ErrorType.Failure);

        public static AppError Validation(string code, string description) =>
            new(code, description, ErrorType.Validation);
    }
}
