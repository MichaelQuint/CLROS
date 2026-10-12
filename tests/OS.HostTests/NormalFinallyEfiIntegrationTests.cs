using OS.Tooling;

internal static class NormalFinallyEfiIntegrationTests
{
    [HostTest("m04.021.normal-finally inactive compiler fixture admission", HostLane.Fast, 0)]
    private static void CompilerFixtureAdmission()
    {
        string root=HostToolProcess.FindRepositoryRoot();
        const string relative=@"Runtime\NormalFinallyProbe.cs";
        string source=File.ReadAllText(Path.Combine(root,"src/OS",relative));
        var project=EfiProjectAdmission.InspectStatic(root);
        HostCheck.True(project.Succeeded,project.Reason+":"+project.Detail);
        HostCheck.True(EfiSourceAdmission.Inspect(source,relative).Succeeded,"exact reviewed fixture");
        HostCheck.True(source.StartsWith("#if NOSTDLIB_EFI && M04_NORMAL_FINALLY_PROBE",StringComparison.Ordinal),"dedicated inactive fixture, no ancestor anchoring");
        foreach(var (from,to) in new[]{("(MethodImplOptions)0x40","(MethodImplOptions)0"),
            ("NormalThrower(value);","NormalBody(value);"),
            ("stamp = value.Payload + 2;","stamp = value.Payload + 3;"),
            ("CLROS_NormalFinally_CompilerAddress","CLROS_Unreviewed_CompilerAddress")})
        {
            string changed=source.Replace(from,to,StringComparison.Ordinal);
            HostCheck.True(changed!=source,"control changes source");
            var rejected=EfiSourceAdmission.Inspect(changed,relative);
            HostCheck.True(!rejected.Succeeded&&rejected.Reason=="forbidden_active_source"&&
                rejected.Detail.EndsWith(":reviewed_normal_finally_source_identity",StringComparison.Ordinal),"fixture control "+from+":"+rejected.Reason);
        }
    }
}
