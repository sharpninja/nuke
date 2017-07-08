﻿// Copyright Matthias Koch 2017.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.Tools.GitLink3;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.NuGet;
using Nuke.Core;
using Nuke.Core.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.GitLink3.GitLink3Tasks;
using static Nuke.Common.Tools.InspectCode.InspectCodeTasks;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;
using static Nuke.Common.Tools.NuGet.NuGetTasks;
using static Nuke.Common.Tools.Xunit2.Xunit2Tasks;
using static Nuke.Core.ControlFlow;
using static Nuke.Core.EnvironmentInfo;

class NukeBuild : GitHubBuild
{
    public static int Main () => Execute<NukeBuild>(x => x.Pack);

    Target Clean => _ => _
            .Executes(() => DeleteDirectories(GlobDirectories(SolutionDirectory, "*/bin", "*/obj")))
            .Executes(() => PrepareCleanDirectory(OutputDirectory));

    Target Restore => _ => _
            .DependsOn(Clean)
            .Executes(() => MSBuild(s => DefaultSettings.MSBuildRestore));

    Target Compile => _ => _
            .DependsOn(Restore)
            .Executes(() => MSBuild(s =>
                (IsWin
                    ? DefaultSettings.MSBuildCompileWithAssemblyInfo
                    : DefaultSettings.MSBuildCompile)
                // TODO: AddLogger(Variable("ija"), ifNotNull: true)));
                .AddLoggers(new[] { Variable("CUSTOM_LOGGER") }.WhereNotNull())));

    Target Link => _ => _
            .OnlyWhen(() => false)
            .DependsOn(Compile)
            .Executes(() => GlobFiles(SolutionDirectory, $"*/bin/{Configuration}/*/*.pdb")
                    .Where(x => !x.Contains("ToolGenerator"))
                    .ForEach(x => GitLink3(s => DefaultSettings.GitLink3.SetPdbFile(x))));

    Target Pack => _ => _
            .DependsOn(Restore, Link)
            .Executes(() => MSBuild(s => DefaultSettings.MSBuildPack));

    Target Push => _ => _
            .DependsOn(Pack)
            .Executes(() => GlobFiles(OutputDirectory, "*.nupkg")
                    .Where(x => !x.EndsWith("symbols.nupkg"))
                    .ForEach(x => SuppressErrors(() =>
                        NuGetPush(s => s
                                .SetVerbosity(NuGetVerbosity.Detailed)
                                .SetTargetPath(x)
                                .SetApiKey(EnsureVariable("MYGET_API_KEY"))
                                .SetSource("https://www.myget.org/F/nukebuild/api/v2/package")))));

    Target Analysis => _ => _
            .DependsOn(Restore)
            .Executes(() => InspectCode(s => DefaultSettings.InspectCode));

    Target Test => _ => _
            .DependsOn(Compile)
            .Executes(() => Xunit2(GlobFiles(SolutionDirectory, $"*/bin/{Configuration}/net4*/Nuke.*.Tests.dll")));

    Target Full => _ => _
            .DependsOn(Compile, Test, Analysis, Push);
}
