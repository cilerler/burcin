function DoWork {
	[CmdletBinding()]
	Param
	(
		[Parameter(Mandatory = $true, Position = 0)]
		[string]$githubToken,

		[Parameter(Mandatory = $true, Position = 1)]
		[string]$githubActor,

		[Parameter(Mandatory = $true, Position = 2)]
		[string]$githubRepository,

		[Parameter(Mandatory = $true, Position = 3)]
		[string]$githubApiUrl,

		[Parameter(Mandatory = $true, Position = 4)]
		[string]$githubEventPath
	)

	$owner = ($githubRepository -split '/')[0];
	$errorCount = $error.Count;

	$event = Get-Content -Path $githubEventPath | ConvertFrom-Json;
	$author = $event.issue ? $event.issue.user.node_id : $event.pull_request.user.node_id;

	if ($null -eq $author) {
		throw 'No user id found';
	}
 else {
		Write-Output 'User id found';
	}

	# gh auth status;

	Write-Output "Looking up sponsorship from $githubActor ...";

	$query = gh api graphql --paginate -f owner=$owner -f query='
query($owner:  String!, $endCursor: String) {
  organization (login: $owner) {
    sponsorshipsAsMaintainer (first: 100, after: $endCursor) {
      nodes {
        sponsorEntity {
          ... on Organization { id, name }
          ... on User { id, name }
        }
        tier { monthlyPriceInDollars }
      }
      pageInfo {
        hasNextPage
        endCursor
      }
    }
  }
}
';

	$amount =	$query |
						ConvertFrom-Json |
						Select-Object @{ Name = 'nodes'; Expression = { $_.data.organization.sponsorshipsAsMaintainer.nodes } } |
						Select-Object -ExpandProperty nodes |
						Where-Object { $_.sponsorEntity.id -eq $author } |
						Select-Object -ExpandProperty tier |
						Select-Object -ExpandProperty monthlyPriceInDollars;

	if ($null -eq $amount) {
		Write-Output "Author is not a sponsor! Nothing left to do.";
		return;
	}

	Write-Output "Author is a sponsor!";

	$headers = @{ 'Accept' = 'application/vnd.github.v3+json;charset=utf-8'; 'Authorization' = "bearer $githubToken" };
	$prefix = "$githubApiUrl/repos/$githubRepository";

	Invoke-WebRequest -Body '{ "name":"sponsor :purple_heart:", "color":"ea4aaa", "description":"sponsor" }' "$prefix/labels" -Method Post -Headers $headers -SkipHttpErrorCheck -UseBasicParsing | Select-Object -ExpandProperty StatusCode;
	Invoke-WebRequest -Body '{ "name":"sponsor :yellow_heart:", "color":"ea4aaa", "description":"sponsor++" }' "$prefix/labels" -Method Post -Headers $headers -SkipHttpErrorCheck -UseBasicParsing | Select-Object -ExpandProperty StatusCode;

	$number = $event.issue ? $event.issue.number : $event.pull_request.number;
	$labels = $amount -ge 100 ? '{"labels":["sponsor :yellow_heart:"]}' : '{"labels":["sponsor :purple_heart:"]}';

	Invoke-WebRequest -Body $labels "$prefix/issues/$number/labels" -Method Post -Headers $headers -SkipHttpErrorCheck -UseBasicParsing | Select-Object -ExpandProperty StatusCode;

	Write-Output 'Label applied';
}

$repositoryName = ($env:GITHUB_REPOSITORY -split '/')[-1];
$timestamp = Get-Date -Format s | ForEach-Object { $_ -replace "[-:]", "" };
$logFile = "/tmp/$repositoryName/$timestamp.txt";
Start-Transcript -Path $logFile;
try {
	DoWork -githubToken $env:GITHUB_TOKEN -githubActor $env:GITHUB_ACTOR -githubRepository $env:GITHUB_REPOSITORY -githubApiUrl $env:GITHUB_API_URL -githubEventPath $env:GITHUB_EVENT_PATH;
}
catch {
	throw;
}
finally {
	Stop-Transcript;
}
