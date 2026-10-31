namespace AgriDrone.Modules.Surveys.Application.Features.Requests.Common;

public static class SurveyRequestReviewChecklistDefinition
{
    public const string Version = "survey-request-review.v7";
    public const string ContactValidCode = "contact-valid";
    public const string DragonFruitFarmConfirmedCode =
        "dragon-fruit-farm-confirmed";
    public const string ServiceAreaSupportedCode =
        "service-area-supported";
    public const string CandidateLocationSufficientCode =
        "candidate-location-sufficient";

    public static IReadOnlyList<SurveyRequestReviewChecklistItem> Items { get; } =
    [
        new(
            ContactValidCode,
            "Contact and applicant information is valid."),
        new(
            DragonFruitFarmConfirmedCode,
            "The proposed or existing Farm is a dragon-fruit Farm."),
        new(
            ServiceAreaSupportedCode,
            "The Farm is within a supported service area."),
        new(
            CandidateLocationSufficientCode,
            "The intake location is sufficient for preliminary review and is not an approved Farm boundary.")
    ];

    private static IReadOnlyList<string> RequiredCodes { get; } = Items
        .Select(item => item.Code)
        .OrderBy(code => code, StringComparer.Ordinal)
        .ToArray();

    public static bool HasExactAnswers(
        IReadOnlyDictionary<string, bool>? answers) =>
        answers is not null &&
        answers.Keys
            .OrderBy(code => code, StringComparer.Ordinal)
            .SequenceEqual(RequiredCodes, StringComparer.Ordinal);

    public static bool AreAllAnswersConfirmed(
        IReadOnlyDictionary<string, bool>? answers) =>
        HasExactAnswers(answers) &&
        RequiredCodes.All(code => answers![code]);

    public static IReadOnlyDictionary<string, bool> CanonicalizeAnswers(
        IReadOnlyDictionary<string, bool> answers)
    {
        ArgumentNullException.ThrowIfNull(answers);

        var canonicalAnswers = new SortedDictionary<string, bool>(
            StringComparer.Ordinal);
        foreach (var answer in answers)
        {
            canonicalAnswers.Add(answer.Key, answer.Value);
        }

        return canonicalAnswers;
    }
}

public sealed record SurveyRequestReviewChecklistItem(
    string Code,
    string Description);

public sealed record SurveyRequestReviewChecklistInput(
    string Version,
    IReadOnlyDictionary<string, bool> Answers,
    string? Notes);
