using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AgriDrone.Api.Contracts.Surveys.Orders;

public sealed class VerifySurveyCaptureAuthorizationForm
{
    /// <summary>
    /// File ảnh (JPG, PNG) hoặc văn bản scan (PDF) bằng chứng thật.
    /// </summary>
    public IFormFile? File { get; init; }

    /// <summary>
    /// Mã định danh mock khi thử nghiệm/demo (ví dụ: LAB-PERMIT-2026).
    /// Bắt buộc nếu không tải file.
    /// </summary>
    [StringLength(200)]
    public string? MockReference { get; init; }

    [Required]
    public DateTimeOffset IssuedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    [Required]
    public uint ExpectedVersion { get; init; }
}
