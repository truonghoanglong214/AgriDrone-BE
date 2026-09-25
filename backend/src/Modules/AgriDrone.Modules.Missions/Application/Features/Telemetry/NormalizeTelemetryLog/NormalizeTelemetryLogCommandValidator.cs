using FluentValidation;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Missions.Application.Features.Telemetry.NormalizeTelemetryLog
{
    internal sealed class NormalizeTelemetryLogCommandValidator : AbstractValidator<NormalizeTelemetryLogCommand>
    {
        public NormalizeTelemetryLogCommandValidator()
        {
            RuleFor(x => x.FarmId)
                .NotEmpty().WithMessage("FarmId is required.");
            RuleFor(x => x.MissionId)
                .NotEmpty().WithMessage("MissionId is required.");
            RuleFor(x => x.SourceFileName)
                .NotEmpty().WithMessage("SourceFileName is required.");
            RuleFor(x => x.Content)
                .NotNull().WithMessage("Content stream is required.");
        }
    }
}
