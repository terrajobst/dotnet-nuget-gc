# dotnet-nuget-gc

[![Build Status](https://terrajobst.visualstudio.com/dotnet-nuget-gc/_apis/build/status/terrajobst.dotnet-nuget-gc?branchName=master)](https://terrajobst.visualstudio.com/dotnet-nuget-gc/_build/latest?definitionId=15)

This `dotnet` extension is designed to clean-up the NuGet cache. It's a
(hopefully) temporary workaround for the [missing cache-expiration
policy][nuget-issue]. Code written by [@dotmorten] as outlined in [his
comment][code-origin].

[@dotmorten]: https://githun.com/dotMorten
[nuget-issue]: https://github.com/NuGet/Home/issues/4980
[code-origin]: https://github.com/NuGet/Home/issues/4980#issuecomment-432512640

## Installation

    $ dotnet tool install dotnet-nuget-gc -g

## Usage

    usage: dotnet nuget-gc [options]

    Options:
      -f, --force                Performs the actual clean-up. Default is to do a
                                 dry-run and report the clean-up that would be
                                 done.
      -m, --min-days=VALUE       Number of days a package must not be used in order
                                 to be purged from the cache. Defaults to 30.
      -?, -h, --help             show this message and exit
