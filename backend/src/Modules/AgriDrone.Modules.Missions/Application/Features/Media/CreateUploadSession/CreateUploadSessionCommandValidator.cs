using AgriDrone.Modules.Missions.Domain.Media;
using FluentValidation;

namespace AgriDrone.Modules.Missions.Application
    .Features.Media.CreateUploadSession;

internal sealed class CreateUploadSessionCommandValidator
    : AbstractValidator<CreateUploadSessionCommand>
{
    private const long MaxImageSizeBytes =
        50L * 1024 * 1024;

    private const long MaxVideoSizeBytes =
        5L * 1024 * 1024 * 1024;

    private static readonly HashSet<string> ImageMimeTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png"
        };

    private static readonly HashSet<string> VideoMimeTypes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "video/mp4",
            "video/quicktime"
        };

    public CreateUploadSessionCommandValidator()
    {
        RuleFor(command => command.TenantId)
            .NotEmpty();

        RuleFor(command => command.FarmId)
            .NotEmpty();

        RuleFor(command => command.MissionId)
            .NotEmpty();

        RuleFor(command => command.OperationId)
            .NotEmpty();

        RuleFor(command => command.ExpectedMissionVersion)
            .GreaterThan(0u);

        RuleFor(command => command.MediaType)
            .IsInEnum();

        RuleFor(command => command.FileName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(255)
            .Must(IsPlainFileName)
            .WithMessage(
                "FileName must be a plain file name without path characters.");

        RuleFor(command => command.MimeType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(100)
            .Must((command, mimeType) =>
                IsCompatibleMimeType(
                    command.MediaType,
                    mimeType))
            .WithMessage(
                "MimeType is not supported for the selected media type.");

        RuleFor(command => command.FileSizeBytes)
            .GreaterThan(0)
            .Must((command, size) =>
                IsSupportedSize(
                    command.MediaType,
                    size))
            .WithMessage(
                "Images must be at most 50 MiB and videos at most 5 GiB.");

        RuleFor(command => command.Sha256Checksum)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .Length(64)
            .Matches("^[0-9a-fA-F]{64}$")
            .WithMessage(
                "Sha256Checksum must contain 64 hexadecimal characters.");
    }

    private static bool IsPlainFileName(
        string fileName)
    {
        return fileName is not "." and not ".." &&
               !fileName.Contains('/') &&
               !fileName.Contains('\\') &&
               !fileName.Any(char.IsControl);
    }

    private static bool IsCompatibleMimeType(
        MediaType mediaType,
        string mimeType)
    {
        return mediaType switch
        {
            MediaType.Image =>
                ImageMimeTypes.Contains(mimeType.Trim()),

            MediaType.Video =>
                VideoMimeTypes.Contains(mimeType.Trim()),

            _ => false
        };
    }

    private static bool IsSupportedSize(
        MediaType mediaType,
        long size)
    {
        return mediaType switch
        {
            MediaType.Image =>
                size is > 0 and <= MaxImageSizeBytes,

            MediaType.Video =>
                size is > 0 and <= MaxVideoSizeBytes,

            _ => false
        };
    }
}