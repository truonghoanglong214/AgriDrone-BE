<#
.SYNOPSIS
Uploads every JPEG/PNG in one folder (including subfolders) to a mission.
.DESCRIPTION
Capture time is optional. Use CapturedAt for one shared UTC instant, or
CaptureTimesCsv with RelativePath and CapturedAt columns for per-image UTC
times. Already completed media are reused on retry.
.EXAMPLE
./Upload-MissionImageFolder.ps1 -FolderPath 'D:/flight/photos' -ApiBaseUrl 'http://localhost:5000' -FarmId $farmId -MissionId $missionId -AccessToken $token
#>
param(
    [Parameter(Mandatory)] [string] $FolderPath,
    [Parameter(Mandatory)] [string] $ApiBaseUrl,
    [Parameter(Mandatory)] [guid] $FarmId,
    [Parameter(Mandatory)] [guid] $MissionId,
    [Parameter(Mandatory)] [string] $AccessToken,
    [string] $CapturedAt,
    [string] $CaptureTimesCsv
)

$ErrorActionPreference = 'Stop'

$folder = (Resolve-Path -LiteralPath $FolderPath).Path
$defaultTimestamp = $null
if ($CapturedAt) {
    $parsed = [datetimeoffset]::Parse($CapturedAt,
        [Globalization.CultureInfo]::InvariantCulture)
    if ($parsed.Offset -ne [timespan]::Zero) {
        throw 'CapturedAt must be UTC, for example 2026-10-13T10:12:44Z.'
    }
    $defaultTimestamp = $parsed.ToString('yyyy-MM-ddTHH:mm:ss.fffZ',
        [Globalization.CultureInfo]::InvariantCulture)
}
$captureTimes = [System.Collections.Generic.Dictionary[string,string]]::new(
    [StringComparer]::OrdinalIgnoreCase)
if ($CaptureTimesCsv) {
    foreach ($row in (Import-Csv -LiteralPath $CaptureTimesCsv)) {
        if (-not $row.RelativePath -or -not $row.CapturedAt) {
            throw 'CSV columns RelativePath and CapturedAt are required.'
        }
        $parsed = [datetimeoffset]::Parse($row.CapturedAt,
            [Globalization.CultureInfo]::InvariantCulture)
        if ($parsed.Offset -ne [timespan]::Zero) {
            throw "CapturedAt must be UTC for $($row.RelativePath)."
        }
        $key = $row.RelativePath.Replace('\', '/')
        $captureTimes[$key] = $parsed.ToString('yyyy-MM-ddTHH:mm:ss.fffZ',
            [Globalization.CultureInfo]::InvariantCulture)
    }
}

function Get-CapturedAt([System.IO.FileInfo] $image) {
    $relativePath = [System.IO.Path]::GetRelativePath(
        $folder, $image.FullName).Replace('\', '/')
    if ($captureTimes.ContainsKey($relativePath)) {
        return $captureTimes[$relativePath]
    }
    if ($defaultTimestamp) { return $defaultTimestamp }
    if ($CaptureTimesCsv) { throw "No CapturedAt provided for $relativePath." }
    return $null
}

$images = @(Get-ChildItem -LiteralPath $folder -File -Recurse |
    Where-Object { $_.Extension.ToLowerInvariant() -in @('.jpg', '.jpeg', '.png') } |
    Sort-Object FullName)
if ($images.Count -eq 0) { throw 'The folder contains no JPEG or PNG images.' }

$base = $ApiBaseUrl.TrimEnd('/')
$mediaUrl = "$base/api/missions/$MissionId/farms/$FarmId/media"
$maxBatchBytes = 49L * 1024 * 1024
$maxImageBytes = 50L * 1024 * 1024
$batch = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
$batchBytes = 0L
$completed = 0

foreach ($image in $images) {
    if ($image.Length -eq 0) { throw "Empty image: $($image.FullName)" }
    if ($image.Length -gt $maxImageBytes) {
        throw "Image exceeds the API 50 MiB limit: $($image.FullName)"
    }
    [void](Get-CapturedAt $image)
}

$client = [System.Net.Http.HttpClient]::new()
$client.Timeout = [timespan]::FromMinutes(10)
$client.DefaultRequestHeaders.Authorization =
    [System.Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $AccessToken)
$storageClient = [System.Net.Http.HttpClient]::new()
$storageClient.Timeout = [timespan]::FromMinutes(10)

function Read-Response($response) {
    $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if (-not $response.IsSuccessStatusCode) {
        throw "HTTP $([int]$response.StatusCode): $body"
    }
    if ([string]::IsNullOrWhiteSpace($body)) { return $null }
    return ConvertFrom-Json -InputObject $body
}

function Send-Batch {
    if ($batch.Count -eq 0) { return }
    $form = [System.Net.Http.MultipartFormDataContent]::new()
    try {
        foreach ($image in $batch) {
            $stream = [System.IO.File]::OpenRead($image.FullName)
            $part = [System.Net.Http.StreamContent]::new($stream)
            $part.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new(
                $(if ($image.Extension.ToLowerInvariant() -eq '.png') { 'image/png' } else { 'image/jpeg' }))
            $form.Add($part, 'Files', $image.Name)
            $imageCapturedAt = Get-CapturedAt $image
            if ($imageCapturedAt) {
                $form.Add([System.Net.Http.StringContent]::new($imageCapturedAt), 'CapturedAt')
            }
        }
        $response = $client.PostAsync($mediaUrl, $form).GetAwaiter().GetResult()
        try {
            $results = Read-Response $response
            $failures = @($results | Where-Object { $_.status -ne 'Completed' })
            $script:completed += @($results | Where-Object { $_.status -eq 'Completed' }).Count
            if ($failures.Count -gt 0) {
                $failures | Format-Table index, fileName, status, errorCode, errorMessage | Out-Host
                throw 'The batch was only partly uploaded. Re-run the command to resume.'
            }
        }
        finally { $response.Dispose() }
    }
    finally {
        $form.Dispose()
        $batch.Clear()
        $script:batchBytes = 0L
    }
}

function Send-LargeImage([System.IO.FileInfo] $image) {
    $detailsResponse = $client.GetAsync(
        "$base/api/farms/$FarmId/missions/$MissionId").GetAwaiter().GetResult()
    try { $mission = Read-Response $detailsResponse }
    finally { $detailsResponse.Dispose() }

    $checksum = (Get-FileHash -LiteralPath $image.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $operationId = [guid]::ParseExact($checksum.Substring(0, 32), 'N')
    $mime = if ($image.Extension.ToLowerInvariant() -eq '.png') { 'image/png' } else { 'image/jpeg' }
    $request = @{
        operationId = $operationId
        fileName = $image.Name
        mimeType = $mime
        mediaType = 0
        fileSizeBytes = $image.Length
        sha256Checksum = $checksum
        expectedMissionVersion = $mission.version
    }
    $imageCapturedAt = Get-CapturedAt $image
    if ($imageCapturedAt) { $request.capturedAt = $imageCapturedAt }
    $request = $request | ConvertTo-Json -Compress
    $json = [System.Net.Http.StringContent]::new(
        $request, [Text.Encoding]::UTF8, 'application/json')
    try {
        $createResponse = $client.PostAsync(
            "$mediaUrl/upload-sessions", $json).GetAwaiter().GetResult()
        try { $session = Read-Response $createResponse }
        finally { $createResponse.Dispose() }
    }
    finally { $json.Dispose() }

    if ($session.status -ne 'Completed') {
        $stream = [System.IO.File]::OpenRead($image.FullName)
        $content = [System.Net.Http.StreamContent]::new($stream)
        $content.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new($mime)
        try {
            $uploadResponse = $storageClient.PutAsync(
                $session.uploadUri, $content).GetAwaiter().GetResult()
            try { [void](Read-Response $uploadResponse) }
            finally { $uploadResponse.Dispose() }
        }
        finally { $content.Dispose() }
    }

    $empty = [System.Net.Http.StringContent]::new('')
    try {
        $completeResponse = $client.PostAsync(
            "$mediaUrl/upload-sessions/$($session.uploadSessionId)/complete",
            $empty).GetAwaiter().GetResult()
        try { [void](Read-Response $completeResponse) }
        finally { $completeResponse.Dispose() }
    }
    finally { $empty.Dispose() }
    $script:completed++
}

try {
    foreach ($image in $images) {
        if ($image.Length -gt $maxBatchBytes) {
            Send-Batch
            Send-LargeImage $image
            continue
        }
        if ($batch.Count -ge 20 -or $batchBytes + $image.Length -gt $maxBatchBytes) {
            Send-Batch
        }
        $batch.Add($image)
        $batchBytes += $image.Length
    }
    Send-Batch
    Write-Output "Uploaded $completed/$($images.Count) images from $folder"
}
finally {
    $client.Dispose()
    $storageClient.Dispose()
}
