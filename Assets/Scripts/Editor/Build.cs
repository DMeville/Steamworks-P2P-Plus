using UnityEngine;
using System.Collections;
using UnityEditor;
using UnityEditor.Callbacks;

public class Build
{
    [MenuItem( "Build/All" )]
    public static void RunBuilds()
    {
        var levels = new[] { "Assets/Test.unity" };

        BuildPipeline.BuildPlayer( levels, "Build/Win32/fpsw_w32.exe", BuildTarget.StandaloneWindows, BuildOptions.AllowDebugging | BuildOptions.Development );
        BuildPipeline.BuildPlayer( levels, "Build/Win64/fpsw_w64.exe", BuildTarget.StandaloneWindows64, BuildOptions.AllowDebugging | BuildOptions.Development );
    }
}
