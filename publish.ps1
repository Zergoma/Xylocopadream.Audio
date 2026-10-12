<#
.SYNOPSIS
    Packs every library under src/ into the local NuGet feed, and optionally pushes them to nuget.org.

.DESCRIPTION
    NuGet caches a package by id and version (%UserProfile%\.nuget\packages), and a version pushed to nuget.org
    can never be replaced: publishing the same version twice would leave apps on a stale copy. The script therefore
    refuses to pack a version already in the local feed; pass -Bump to increment the patch number of <Version>
    in Directory.Build.props first.

    -NuGetOrg runs the tests, then pushes the packages of the version to nuget.org with the API key of the
    NUGET_API_KEY environment variable (never stored in the repository). Without -Bump, it pushes the packages
    already in the local feed: a push that failed can be run again (versions already on nuget.org are skipped).

.EXAMPLE
    .\publish.ps1 -Bump

.EXAMPLE
    $env:NUGET_API_KEY = '...'; .\publish.ps1 -Bump -NuGetOrg
#>
param(
    [switch] $Bump,
    [switch] $NuGetOrg,
    [string] $Feed = (Join-Path $env:USERPROFILE 'nuget_local')
)

$ErrorActionPreference = 'Stop'
$nuGetOrgSource = 'https://api.nuget.org/v3/index.json'

# checked first: nothing is bumped nor packed when the push cannot happen
if ($NuGetOrg -and [string]::IsNullOrWhiteSpace($env:NUGET_API_KEY)) {
    throw 'Set the NUGET_API_KEY environment variable to push to nuget.org.'
}

# a package links to the commit it was built from: on nuget.org it must be the code that was committed
# (the version bump of Directory.Build.props is the only change allowed, it is committed afterwards)
if ($NuGetOrg) {
    $changes = @(git -C $PSScriptRoot status --porcelain) | Where-Object { $_ -notmatch 'Directory\.Build\.props$' }
    if ($changes.Count -gt 0) {
        throw "Commit the changes first, a package on nuget.org must match a commit:`n$($changes -join "`n")"
    }
}

$props = Join-Path $PSScriptRoot 'Directory.Build.props'
$text = [IO.File]::ReadAllText($props)
$match = [regex]::Match($text, '<Version>(\d+)\.(\d+)\.(\d+)</Version>')
if (-not $match.Success) {
    throw 'Directory.Build.props has no <Version>major.minor.patch</Version>.'
}

$major, $minor, $patch = [int]$match.Groups[1].Value, [int]$match.Groups[2].Value, [int]$match.Groups[3].Value
if ($Bump) {
    $patch++
    $text = $text.Remove($match.Index, $match.Length).Insert($match.Index, "<Version>$major.$minor.$patch</Version>")
    [IO.File]::WriteAllText($props, $text, (New-Object Text.UTF8Encoding $false))
}

$version = "$major.$minor.$patch"
$projects = Get-ChildItem (Join-Path $PSScriptRoot 'src') -Recurse -Filter *.csproj

# the packages of this version in the local feed; a project may add a suffix to the version (e.g. 0.1.2-preview)
function Get-Packages($project) {
    @(Get-ChildItem $Feed -Filter "$($project.BaseName).$version.nupkg" -ErrorAction SilentlyContinue) +
    @(Get-ChildItem $Feed -Filter "$($project.BaseName).$version-*.nupkg" -ErrorAction SilentlyContinue)
}

$packed = @($projects | Where-Object { (Get-Packages $_).Count -gt 0 })
$pushExisting = $NuGetOrg -and -not $Bump -and $packed.Count -eq $projects.Count

if (-not $pushExisting) {
    if ($packed.Count -gt 0) {
        throw "Version $version is already in $Feed. Run .\publish.ps1 -Bump to publish a new version."
    }

    if ($NuGetOrg) {
        dotnet test (Join-Path $PSScriptRoot 'Xylocopadream.Audio.slnx') -c Release --nologo
        if ($LASTEXITCODE -ne 0) {
            throw 'Tests failed: nothing is published.'
        }
    }

    New-Item -ItemType Directory -Force $Feed | Out-Null
    foreach ($project in $projects) {
        # deterministic build: source paths mapped to the repository, so debugging steps into the published code
        dotnet pack $project.FullName -c Release -o $Feed --nologo -p:ContinuousIntegrationBuild=true
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet pack failed for $($project.Name)."
        }
    }

    Write-Host "Published $version to $Feed"
}

if ($NuGetOrg) {
    foreach ($project in $projects) {
        foreach ($package in Get-Packages $project) {
            dotnet nuget push $package.FullName --api-key $env:NUGET_API_KEY --source $nuGetOrgSource --skip-duplicate
            if ($LASTEXITCODE -ne 0) {
                throw "Push to nuget.org failed for $($package.Name)."
            }
        }
    }

    Write-Host "Pushed $version to nuget.org (validation and indexing take a few minutes)"
}
