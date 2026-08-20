using AgriDrone.IntegrationContracts.Contracts;
using AgriDrone.IntegrationContracts.Messaging;

namespace AgriDrone.IntegrationContracts.Mapping.Validation;

public static class MappingCandidatesApprovedV1Validator
{
    public static IReadOnlyList<string> Validate(
        MappingCandidatesApprovedV1? payload)
    {
        var errors = new List<string>();

        if (payload is null)
        {
            errors.Add("Payload is required.");
            return errors;
        }

        ValidateHeader(payload, errors);
        ValidateParameters(payload.Parameters, errors);
        ValidateCandidates(payload.Candidates, errors);

        return errors;
    }

    private static void ValidateHeader(
        MappingCandidatesApprovedV1 payload,
        List<string> errors)
    {
        if (payload.ApprovalId == Guid.Empty)
        {
            errors.Add("ApprovalId is required.");
        }

        if (payload.MissionId == Guid.Empty)
        {
            errors.Add("MissionId is required.");
        }

        if (payload.FarmId == Guid.Empty)
        {
            errors.Add("FarmId is required.");
        }

        if (payload.ZoneId == Guid.Empty)
        {
            errors.Add("ZoneId is required.");
        }

        if (payload.ExpectedCurrentMapVersionId == Guid.Empty)
        {
            errors.Add(
                "ExpectedCurrentMapVersionId cannot be an empty GUID when provided.");
        }

        if (string.IsNullOrWhiteSpace(payload.AlgorithmVersion))
        {
            errors.Add("AlgorithmVersion is required.");
        }
        else if (payload.AlgorithmVersion.Length >
                 IntegrationContractLimits.MaximumAlgorithmVersionLength)
        {
            errors.Add(
                $"AlgorithmVersion cannot exceed {IntegrationContractLimits.MaximumAlgorithmVersionLength} characters.");
        }

        if (!double.IsFinite(payload.GridBearingDeg) ||
            payload.GridBearingDeg < 0 ||
            payload.GridBearingDeg >= 360)
        {
            errors.Add("GridBearingDeg must be in the range [0, 360).");
        }

        if (!double.IsFinite(payload.RowSpacingM) || payload.RowSpacingM <= 0)
        {
            errors.Add("RowSpacingM must be a finite number greater than zero.");
        }

        if (!double.IsFinite(payload.PlantSpacingM) || payload.PlantSpacingM <= 0)
        {
            errors.Add("PlantSpacingM must be a finite number greater than zero.");
        }
    }

    private static void ValidateParameters(
        IReadOnlyDictionary<string, string>? parameters,
        List<string> errors)
    {
        if (parameters is null)
        {
            errors.Add("Parameters is required.");
            return;
        }

        if (parameters.Count > IntegrationContractLimits.MaximumParameterCount)
        {
            errors.Add(
                $"Parameters cannot contain more than {IntegrationContractLimits.MaximumParameterCount} items.");
        }

        foreach (var parameter in parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Key))
            {
                errors.Add("Parameters cannot contain an empty key.");
            }
            else if (parameter.Key.Length >
                     IntegrationContractLimits.MaximumParameterKeyLength)
            {
                errors.Add(
                    $"Parameter key '{parameter.Key}' cannot exceed {IntegrationContractLimits.MaximumParameterKeyLength} characters.");
            }

            if (parameter.Value is null)
            {
                errors.Add($"Parameters['{parameter.Key}'] cannot be null.");
            }
            else if (parameter.Value.Length >
                     IntegrationContractLimits.MaximumParameterValueLength)
            {
                errors.Add(
                    $"Parameters['{parameter.Key}'] cannot exceed {IntegrationContractLimits.MaximumParameterValueLength} characters.");
            }
        }
    }

    private static void ValidateCandidates(
        IReadOnlyList<MappingCandidateV1>? candidates,
        List<string> errors)
    {
        if (candidates is null || candidates.Count == 0)
        {
            errors.Add("Candidates must contain at least one item.");
            return;
        }

        if (candidates.Count >
            IntegrationContractLimits.MaximumMappingCandidateCount)
        {
            errors.Add(
                $"Candidates cannot contain more than {IntegrationContractLimits.MaximumMappingCandidateCount} items.");
            return;
        }

        var observationIds = new HashSet<Guid>();
        var gridPositions = new HashSet<(int RowIndex, int ColumnIndex)>();
        var resolvedPlantIds = new HashSet<Guid>();

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var path = $"Candidates[{index}]";

            if (candidate is null)
            {
                errors.Add($"{path} is required.");
                continue;
            }

            ValidateCandidate(
                candidate,
                path,
                observationIds,
                gridPositions,
                resolvedPlantIds,
                errors);
        }

        var hasActionableCandidate = candidates.Any(candidate =>
            candidate is not null &&
            candidate.Decision is
                MappingCandidateDecisions.Matched or
                MappingCandidateDecisions.CreateNew);

        if (!hasActionableCandidate)
        {
            errors.Add(
                "Candidates must contain at least one matched or create-new item.");
        }
    }

    private static void ValidateCandidate(
        MappingCandidateV1 candidate,
        string path,
        HashSet<Guid> observationIds,
        HashSet<(int RowIndex, int ColumnIndex)> gridPositions,
        HashSet<Guid> resolvedPlantIds,
        List<string> errors)
    {
        if (candidate.ObservationId == Guid.Empty)
        {
            errors.Add($"{path}.ObservationId is required.");
        }
        else if (!observationIds.Add(candidate.ObservationId))
        {
            errors.Add($"{path}.ObservationId is duplicated.");
        }

        if (!double.IsFinite(candidate.Latitude) ||
            candidate.Latitude < -90 ||
            candidate.Latitude > 90)
        {
            errors.Add($"{path}.Latitude must be in the range [-90, 90].");
        }

        if (!double.IsFinite(candidate.Longitude) ||
            candidate.Longitude < -180 ||
            candidate.Longitude > 180)
        {
            errors.Add($"{path}.Longitude must be in the range [-180, 180].");
        }

        if (candidate.RowIndex <= 0)
        {
            errors.Add($"{path}.RowIndex must be greater than zero.");
        }

        if (candidate.ColumnIndex <= 0)
        {
            errors.Add($"{path}.ColumnIndex must be greater than zero.");
        }

        if (candidate.RowIndex > 0 &&
            candidate.ColumnIndex > 0 &&
            !gridPositions.Add((candidate.RowIndex, candidate.ColumnIndex)))
        {
            errors.Add($"{path} has a duplicated row/column position.");
        }

        if (candidate.LocationAccuracyM is double accuracy &&
            (!double.IsFinite(accuracy) || accuracy < 0))
        {
            errors.Add(
                $"{path}.LocationAccuracyM must be a finite, non-negative number.");
        }

        if (!double.IsFinite(candidate.PositionConfidence) ||
            candidate.PositionConfidence < 0 ||
            candidate.PositionConfidence > 1)
        {
            errors.Add(
                $"{path}.PositionConfidence must be in the range [0, 1].");
        }

        ValidateDecision(candidate, path, resolvedPlantIds, errors);
    }

    private static void ValidateDecision(
        MappingCandidateV1 candidate,
        string path,
        HashSet<Guid> resolvedPlantIds,
        List<string> errors)
    {
        switch (candidate.Decision)
        {
            case MappingCandidateDecisions.Matched:
                if (candidate.ResolvedPlantId is not Guid resolvedPlantId ||
                    resolvedPlantId == Guid.Empty)
                {
                    errors.Add(
                        $"{path}.ResolvedPlantId is required for a matched candidate.");
                }
                else if (!resolvedPlantIds.Add(resolvedPlantId))
                {
                    errors.Add(
                        $"{path}.ResolvedPlantId is used by more than one candidate.");
                }

                break;

            case MappingCandidateDecisions.CreateNew:
                if (candidate.ResolvedPlantId.HasValue)
                {
                    errors.Add(
                        $"{path}.ResolvedPlantId must be null when creating a new plant.");
                }

                break;

            case MappingCandidateDecisions.Rejected:
                if (candidate.ResolvedPlantId.HasValue)
                {
                    errors.Add(
                        $"{path}.ResolvedPlantId must be null for a rejected candidate.");
                }

                break;

            default:
                errors.Add(
                    $"{path}.Decision must be 'matched', 'create-new', or 'rejected'.");
                break;
        }
    }
}
