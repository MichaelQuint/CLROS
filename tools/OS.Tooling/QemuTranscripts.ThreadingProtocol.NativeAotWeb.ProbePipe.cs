namespace OS.Tooling;
public static partial class QemuTranscripts
{
    private sealed partial class ThreadingProtocol
    {
        private WebProbePipe? webProbePipe;
        // Literal Smoke's additional NONBLOCK pipe shares the existing fd
        // namespace, but never borrows or completes the runtime signal pipe's
        // blocking operations. Replay owns counts/generations, not copied bytes.
        // Only positive PIPE_BUF-bounded requests with mapped buffers are
        // admitted; this is not a general descriptor compatibility model.
        private bool WebProbePipeCall(Dictionary<string, ulong> r)
        {
            ulong nr = r["nr"], a = r["a0"], b = r["a1"], c = r["a2"], v = r["valueBits"];
            if (nr == 293 && b != 0x80000)
            {
                Need(web && !service && b == 0x80800 && v == 0 && memory.Covered(a, 8, 2) && pipeRead != 0 && pipeWrite != 0 && webProbePipe is null, "web_probe_pipe_create");
                int read = LowestFd(), write = LowestFd(read);
                Need(read >= 3 && write >= 3, "web_probe_pipe_capacity");
                InstallDescriptorGeneration(read);
                InstallDescriptorGeneration(write);
                Need(files.Add(read) && files.Add(write), "web_probe_pipe_descriptor");
                webProbePipe = new(read, write, descriptorGenerations[read], descriptorGenerations[write]);
                return true;
            }

            if (webProbePipe is not { } pipe || a > int.MaxValue || !((pipe.Read != 0 && a == (ulong)pipe.Read) || (pipe.Write != 0 && a == (ulong)pipe.Write)))
                return false;
            bool readEnd = a == (ulong)pipe.Read;
            if (nr is not (0 or 1 or 3))
            {
                // Adding these endpoints to the shared fd inventory must not
                // grant them the ordinary-file or socket operation contracts.
                Need(nr is not (5 or 8 or 16 or 17 or 19 or 20 or 72 or 73 or 221), "web_probe_pipe_unmeasured_operation");
                return false;
            }

            int fd = (int)a;
            Need(web && !service && files.Contains(fd) && descriptorGenerations[fd] == (readEnd ? pipe.ReadGeneration : pipe.WriteGeneration), "web_probe_pipe_generation");
            if (nr == 3)
            {
                Need(v == 0 && files.Remove(fd), "web_probe_pipe_close");
                if (readEnd)
                {
                    ProbeReadClosed(fd, pipe);
                    pipe.Read = 0;
                }
                else
                    pipe.Write = 0;
                if (pipe.Read == 0 && pipe.Write == 0)
                    webProbePipe = null;
                SocketEligibility();
                return true;
            }

            Need(c is> 0 and <= 4096, "web_probe_pipe_count");
            if ((nr == 0) != readEnd)
            {
                Need(v == Bits(-9), "web_probe_pipe_direction");
                return true;
            }

            Need(memory.Covered(b, c, nr == 0 ? 2U : 1U), "web_probe_pipe_buffer");
            long expected = nr == 0 ? pipe.Buffered != 0 ? Math.Min((long)c, pipe.Buffered) : pipe.Write == 0 ? 0 : -11 : pipe.Read == 0 ? -32 : c > (ulong)(4096 - pipe.Buffered) ? -11 : (long)c;
            Need(v == Bits(expected), "web_probe_pipe_result");
            if (expected > 0)
                pipe.Buffered += nr == 0 ? -(int)expected : (int)expected;
            SocketEligibility();
            return true;
        }
    }
}
