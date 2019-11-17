Copied from https://github.com/mono/mono/tree/master/mcs/class/Mono.Posix
added https://github.com/mono/mono/blob/master/mcs/build/common/Locale.cs
commented out assembly signing

Why:

The nuget https://www.nuget.org/packages/Mono.Posix.NETStandard/ is huge.
The reason seems to be that it used Mono as framework and not NetStandard.
When including it in a tool with 3 target frameworks, Mono.Posix is included 3 times in the tool nuget.
So this is was to reduce the size of the nuget package by around 8,5 Mb.