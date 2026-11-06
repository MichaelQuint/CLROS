using OS.Tooling;
using F = OS.Tooling.NativeAotLogUploadContract;

internal static partial class NativeAotLogUploadContractTests
{
    [HostTest("log upload Linux handoff releases real evidence when final pass output throws")]
    private static void LinuxHandoffOutputFailure()
    {
        const string marker = "nativeaot.log-upload-reference";
        using var held = new AuthorityFiles();
        AuthorityFile evidence = CreateLinuxHandoffEvidence(held);
        // Ownership-only shell. Unrelated metadata is deliberately unused;
        // this neither runs Linux nor represents a completed Linux reference.
        using var pending = new F.LinuxReference(evidence.Root, null!, null!, null!, null!, null!, held, [evidence]);
        AssertLinuxHandoffEvidenceHeld(pending, evidence);
        using var output = new FinalPassFaultWriter(marker);
        output.WriteLine("TEST_START " + marker);
        F.LinuxReference? returned = null;
        IOException? failure = null;
        try
        {
            returned = F.PublishLinuxReference(pending, output, marker);
        }
        catch (IOException error)
        {
            failure = error;
        }

        HostCheck.True(returned is null && ReferenceEquals(failure, output.Failure));
        HostCheck.Equal(1, output.FinalPassCalls);
        HostCheck.Equal("TEST_START " + marker + output.NewLine, output.ToString());
        // Assertions precede the using-scope cleanup, so the fixture cannot
        // accidentally supply the production release being tested.
        AssertLinuxHandoffEvidenceReleased(pending, evidence);
    }

    [HostTest("log upload Linux handoff retains real evidence until successful caller disposal")]
    private static void LinuxHandoffOutputSuccess()
    {
        const string marker = "nativeaot.log-upload-reference";
        using var held = new AuthorityFiles();
        AuthorityFile evidence = CreateLinuxHandoffEvidence(held);
        // Ownership-only shell; these unused null fields grant no execution claim.
        using var pending = new F.LinuxReference(evidence.Root, null!, null!, null!, null!, null!, held, [evidence]);
        AssertLinuxHandoffEvidenceHeld(pending, evidence);
        using var output = new StringWriter();
        using F.LinuxReference returned = F.PublishLinuxReference(pending, output, marker);
        HostCheck.True(ReferenceEquals(pending, returned));
        HostCheck.Equal("TEST_PASS " + marker + output.NewLine, output.ToString());
        AssertLinuxHandoffEvidenceHeld(returned, evidence);
        returned.Dispose();
        AssertLinuxHandoffEvidenceReleased(returned, evidence);
    }

    private static AuthorityFile CreateLinuxHandoffEvidence(AuthorityFiles held)
    {
        string root = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "log-upload-handoff-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "evidence.bin");
        File.WriteAllBytes(path, [1, 2, 3]);
        return held.Open(root, path, "host-only handoff ownership evidence", "log_upload_handoff", 3, singleLink: true);
    }

    private static void AssertLinuxHandoffEvidenceHeld(F.LinuxReference result, AuthorityFile evidence)
    {
        result.VerifyUnchanged();
        HostCheck.True(evidence.ReadBytes(3, "log_upload_handoff_read").SequenceEqual(new byte[] { 1, 2, 3 }));
        if (OperatingSystem.IsWindows())
        {
            bool exclusiveRejected = false;
            try
            {
                using var exclusive = new FileStream(evidence.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                exclusiveRejected = true;
            }

            HostCheck.True(exclusiveRejected, "retained evidence must still deny exclusive write access");
        }
    }

    private static void AssertLinuxHandoffEvidenceReleased(F.LinuxReference result, AuthorityFile evidence)
    {
        bool resultDisposed = false;
        try
        {
            result.VerifyUnchanged();
        }
        catch (ObjectDisposedException)
        {
            resultDisposed = true;
        }

        HostCheck.True(resultDisposed, "unreturned or caller-disposed result must release ownership");
        bool evidenceDisposed = false;
        try
        {
            evidence.ReadBytes(3, "log_upload_handoff_read");
        }
        catch (ObjectDisposedException)
        {
            evidenceDisposed = true;
        }

        HostCheck.True(evidenceDisposed, "the actual retained file stream must be closed");
        using var exclusive = new FileStream(evidence.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        HostCheck.Equal(3L, exclusive.Length);
    }
}
