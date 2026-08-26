[CmdletBinding()]
param(
	[Parameter(Mandatory)]
	[string]$SvgPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Set-NaturalTextAnchors {
	param(
		[Parameter(Mandatory)]
		[string]$Content,

		[Parameter(Mandatory)]
		[string]$GroupClass,

		[Parameter(Mandatory)]
		[ValidateSet("middle", "end")]
		[string]$TextAnchor
	)

	$escapedGroupClass = [regex]::Escape($GroupClass)
	$groupPattern = "(?s)<g class=`"$escapedGroupClass`"[^>]*>.*?</g>"
	$groupMatches = [regex]::Matches($Content, $groupPattern)
	if ($groupMatches.Count -ne 1) {
		throw "Expected one '$GroupClass' group; found $($groupMatches.Count)."
	}

	$numberPattern = '[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[Ee][-+]?\d+)?'
	$culture = [System.Globalization.CultureInfo]::InvariantCulture

	return [regex]::Replace($Content, $groupPattern, {
		param($groupMatch)

		[regex]::Replace($groupMatch.Value, '<text(?<attributes>[^>]*)>', {
			param($textMatch)

			$attributes = $textMatch.Groups['attributes'].Value
			$textLengthMatch = [regex]::Match(
				$attributes,
				"\stextLength=`"(?<value>$numberPattern)`"")

			# Blank lines and already-normalized dynamic text do not have a fixed text length.
			if (-not $textLengthMatch.Success) {
				return $textMatch.Value
			}

			$xMatch = [regex]::Match($attributes, "\sx=`"(?<value>$numberPattern)`"")
			if (-not $xMatch.Success) {
				throw "A '$GroupClass' text element has textLength but no numeric x coordinate."
			}

			$sourceX = [decimal]::Parse($xMatch.Groups['value'].Value, $culture)
			$sourceLength = [decimal]::Parse($textLengthMatch.Groups['value'].Value, $culture)
			$anchoredX = if ($TextAnchor -eq "middle") {
				$sourceX + ($sourceLength / 2)
			}
			else {
				$sourceX + $sourceLength
			}

			$x = $anchoredX.ToString('0.############', $culture)
			$attributes = [regex]::Replace(
				$attributes,
				'\s+(?:x|textLength|text-anchor)="[^"]*"',
				'')

			return "<text x=`"$x`" text-anchor=`"$TextAnchor`"$attributes>"
		})
	})
}

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../../.."))
$resolvedSvgPath = if ([System.IO.Path]::IsPathRooted($SvgPath)) {
	[System.IO.Path]::GetFullPath($SvgPath)
}
else {
	[System.IO.Path]::GetFullPath((Join-Path (Get-Location).Path $SvgPath))
}
$projectRootPrefix = $projectRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if (-not $resolvedSvgPath.StartsWith($projectRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
	throw "Refusing to normalize an SVG outside '$projectRoot': '$resolvedSvgPath'."
}
if ([System.IO.Path]::GetExtension($resolvedSvgPath) -ne ".svg") {
	throw "PlantUML normalization requires an .svg file: '$resolvedSvgPath'."
}
if (-not (Test-Path -LiteralPath $resolvedSvgPath -PathType Leaf)) {
	throw "PlantUML SVG was not found: '$resolvedSvgPath'."
}

# Keep these source-template sentinels split so dotnet new does not replace them inside this helper.
$legalNameSentinel = "Burcin" + "Legal"
$classificationSentinel = "BurcinDocument" + "Classification"
$legalNameXmlToken = "(organization-legal-name-" + "xml-encoded)"
$classificationXmlToken = "(document-classification-" + "xml-encoded)"

$svg = [System.IO.File]::ReadAllText($resolvedSvgPath)
$normalizedSvg = $svg.Replace($legalNameSentinel, $legalNameXmlToken)
$normalizedSvg = $normalizedSvg.Replace($classificationSentinel, $classificationXmlToken)
$normalizedSvg = Set-NaturalTextAnchors $normalizedSvg "header" "end"
$normalizedSvg = Set-NaturalTextAnchors $normalizedSvg "footer" "middle"

if ($normalizedSvg -ne $svg) {
	[System.IO.File]::WriteAllText(
		$resolvedSvgPath,
		$normalizedSvg,
		[System.Text.UTF8Encoding]::new($false))
}

try {
	[void][xml]$normalizedSvg
}
catch {
	throw "Normalized PlantUML SVG is not valid XML: '$resolvedSvgPath'. $($_.Exception.Message)"
}
