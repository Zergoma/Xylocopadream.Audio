<#
.SYNOPSIS
    Packs every library under src/ into the local NuGet feed, and optionally pushes them to the private feed of the
    Xylocopadream organization on GitHub Packages. Never to nuget.org: the packages are not public.

.DESCRIPTION
    NuGet caches a package by id and version (%UserProfile%\.nuget\packages), and a version pushed to a feed can
    never be replaced: publishing the same version twice would leave apps on a stale copy. The script therefore
    refuses to pack a version already in the local feed; pass -Bump to increment the patch number of <Version>
    in Directory.Build.props first.

    -GitHub runs the tests, then pushes the packages of the version to https://nuget.pkg.github.com/Xylocopadream
    with the classic token (scope write:packages) of the GITHUB_PACKAGES_TOKEN environment variable.
    The token is never stored in the repository. Without -Bump, -GitHub pushes the packages already in the local
    feed: a push that failed can be run again (versions already in the feed are skipped).

.EXAMPLE
    .\publish.ps1 -Bump

.EXAMPLE
    .\publish.ps1 -GitHub          # the version of Directory.Build.props, to the local feed and GitHub Packages
#>
param(
    [switch] $Bump,
    [switch] $GitHub,
    [string] $Feed = (Join-Path $env:USERPROFILE 'nuget_local')
)

$ErrorActionPreference = 'Stop'

# the remote feeds asked for, each with the environment variable of its key
$targets = @()
if ($GitHub) {
    # GitHub Packages takes no symbol package (.snupkg)
    $targets += [pscustomobject]@{ Name = 'GitHub Packages'; Source = 'https://nuget.pkg.github.com/Xylocopadream/index.json'; KeyVariable = 'GITHUB_PACKAGES_TOKEN'; PushArgs = @('--no-symbols') }
}

# checked first: nothing is bumped nor packed when a push cannot happen
foreach ($target in $targets) {
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($target.KeyVariable))) {
        throw "Set the $($target.KeyVariable) environment variable to push to $($target.Name)."
    }
}

# a package links to the commit it was built from: a pushed package must be the code that was committed
# (the version bump of Directory.Build.props is the only change allowed, it is committed afterwards)
if ($targets.Count -gt 0) {
    $changes = @(git -C $PSScriptRoot status --porcelain) | Where-Object { $_ -notmatch 'Directory\.Build\.props$' }
    if ($changes.Count -gt 0) {
        throw "Commit the changes first, a pushed package must match a commit:`n$($changes -join "`n")"
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
$pushExisting = $targets.Count -gt 0 -and -not $Bump -and $packed.Count -eq $projects.Count

if (-not $pushExisting) {
    if ($packed.Count -gt 0) {
        throw "Version $version is already in $Feed. Run .\publish.ps1 -Bump to publish a new version."
    }

    # dotnet picks its SDK from the global.json of the current directory: the one of this repository, wherever the
    # script is run from
    Push-Location $PSScriptRoot
    try {
        if ($targets.Count -gt 0) {
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
    }
    finally {
        Pop-Location
    }

    Write-Host "Published $version to $Feed"
}

foreach ($target in $targets) {
    $key = [Environment]::GetEnvironmentVariable($target.KeyVariable)
    foreach ($project in $projects) {
        foreach ($package in Get-Packages $project) {
            dotnet nuget push $package.FullName --api-key $key --source $target.Source --skip-duplicate @($target.PushArgs)
            if ($LASTEXITCODE -ne 0) {
                throw "Push to $($target.Name) failed for $($package.Name)."
            }
        }
    }

    Write-Host "Pushed $version to $($target.Name)"
}
