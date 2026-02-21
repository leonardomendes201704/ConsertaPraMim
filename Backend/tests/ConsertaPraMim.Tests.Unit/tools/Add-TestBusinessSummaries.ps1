param(
    [string]$RootPath = (Join-Path $PSScriptRoot ".."),
    [switch]$Apply,
    [switch]$Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not $Apply -and -not $Check) {
    throw "Use -Apply para aplicar alteracoes ou -Check para validar."
}

function Get-DisplayNameFromAttribute {
    param([string]$Line)

    $attrMatch = [System.Text.RegularExpressions.Regex]::Match(
        $Line,
        "^\s*\[(Fact|Theory)\((?<args>.*)\)\]\s*$"
    )
    if (-not $attrMatch.Success) {
        return $null
    }

    $args = $attrMatch.Groups["args"].Value
    $displayMatch = [System.Text.RegularExpressions.Regex]::Match(
        $args,
        "DisplayName\s*=\s*""(?<display>(?:[^""\\]|\\.)*)"""
    )
    if (-not $displayMatch.Success) {
        return $null
    }

    return $displayMatch.Groups["display"].Value.Replace('\"', '"')
}

function Find-AttributeBlockStart {
    param(
        [string[]]$Lines,
        [int]$Index
    )

    $start = $Index
    while ($start -gt 0) {
        $prev = $Lines[$start - 1].Trim()
        if ($prev.StartsWith("[")) {
            $start--
            continue
        }

        break
    }

    return $start
}

function HasXmlSummaryImmediatelyAbove {
    param(
        [string[]]$Lines,
        [int]$Index
    )

    $cursor = $Index - 1
    while ($cursor -ge 0 -and [string]::IsNullOrWhiteSpace($Lines[$cursor])) {
        $cursor--
    }

    if ($cursor -lt 0) {
        return $false
    }

    return $Lines[$cursor].Trim().Equals("/// </summary>", [System.StringComparison]::Ordinal)
}

function Escape-ForXml {
    param([string]$Value)

    return [System.Security.SecurityElement]::Escape($Value)
}

$files = Get-ChildItem $RootPath -Recurse -Filter *.cs |
    Where-Object {
        $_.FullName -notmatch "\\bin\\" -and
        $_.FullName -notmatch "\\obj\\"
    }

$totalTests = 0
$summariesAdded = 0
$missingSummaries = 0
$changedFiles = 0

foreach ($file in $files) {
    $lines = Get-Content $file.FullName
    $changed = $false

    for ($i = 0; $i -lt $lines.Length; $i++) {
        $display = Get-DisplayNameFromAttribute -Line $lines[$i]
        if ([string]::IsNullOrWhiteSpace($display)) {
            continue
        }

        $totalTests++
        $blockStart = Find-AttributeBlockStart -Lines $lines -Index $i

        if (HasXmlSummaryImmediatelyAbove -Lines $lines -Index $blockStart) {
            continue
        }

        $missingSummaries++
        if (-not $Apply) {
            continue
        }

        $indentMatch = [System.Text.RegularExpressions.Regex]::Match($lines[$blockStart], "^(\s*)")
        $indent = if ($indentMatch.Success) { $indentMatch.Groups[1].Value } else { "" }
        $escapedDisplay = Escape-ForXml -Value $display
        $summaryText = "Este teste tem como objetivo validar, em nivel de negocio, o seguinte comportamento: $escapedDisplay."

        $insertLines = @(
            "$indent/// <summary>",
            "$indent/// $summaryText",
            "$indent/// </summary>"
        )

        $lines = @($lines[0..($blockStart - 1)] + $insertLines + $lines[$blockStart..($lines.Length - 1)])
        $i += $insertLines.Length
        $changed = $true
        $summariesAdded++
    }

    if ($Apply -and $changed) {
        Set-Content -Path $file.FullName -Encoding utf8 -Value $lines
        $changedFiles++
    }
}

Write-Output "Total de testes com DisplayName: $totalTests"
Write-Output "Testes sem summary antes da execucao: $missingSummaries"
if ($Apply) {
    Write-Output "Summaries adicionados: $summariesAdded"
    Write-Output "Arquivos alterados: $changedFiles"
}

if ($Check -and $missingSummaries -gt 0) {
    exit 1
}

exit 0
