namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed partial class ThreadingProtocol
    {
        // Measured readonly web file calls. Pointer coverage proves access to
        // observed buffers, not pathname bytes, stat contents or link targets.
        // Settings identity comes from the validated M10 config call and its
        // current open-description generation; fd reuse grants no settings data.
        // complete source/parent joins remain separate admission requirements.
        private readonly Dictionary<int,int> webFileGenerations=[];
        private int webFileGeneration,webSettingsGeneration;

        private bool WebFileCall(Dictionary<string,ulong> r,LinuxCallWaitObservation witness)
        {
            ulong nr=r["nr"],a=r["a0"],b=r["a1"],c=r["a2"],d=r["a3"],v=r["valueBits"];
            bool file=a<=int.MaxValue && files.Contains((int)a) && !socketDescriptors.ContainsKey((int)a);
            switch(nr)
            {
                case 4: case 6:
                    Need(memory.Covered(a,1,1) && (v==Bits(-2) || v==0 && memory.Covered(b,144,3)),
                        "web_path_stat");
                    break;
                case 5:
                    Need(file && v==0 && memory.Covered(b,144,3),"web_file_stat");
                    break;
                case 17:
                    Need(file && webSettingsFd>=3 && a==(ulong)webSettingsFd && c<=65536 && d<=long.MaxValue,
                        "web_settings_pread_arguments");
                    Need(webFileGenerations.TryGetValue((int)a,out int generation),"web_settings_file_generation");
                    if((witness.Identity,witness.Ordinal)==webSettingsCall)
                    {
                        Need(webSettingsGeneration==0,"web_settings_duplicate_binding");webSettingsGeneration=generation;
                    }
                    Need(webSettingsGeneration!=0 && generation==webSettingsGeneration,"web_settings_file_generation");
                    ulong bytes=d>=webSettingsBytes?0:Math.Min(c,Math.Min(8192UL,webSettingsBytes-d));
                    Need(v==bytes && (bytes==0 || memory.Covered(b,bytes,3)),"web_settings_pread_result");
                    // Positioned reads do not advance an open-description cursor.
                    break;
                case 73:
                    Need(file && b==5 && v==Bits(-38),"web_flock_unsupported");
                    break;
                case 79:
                    Need(b>=2 && v==2 && memory.Covered(a,2,3),"web_root_cwd");
                    break;
                case 89:
                    ulong executablePathBytes=service?(ulong)("/app/"+NativeAotServicePackage.ApplicationPath).Length:25;
                    Need(c is >0 and <=4096 && memory.Covered(a,1,1) &&
                        (v==Bits(-22) || (v==1 || v==executablePathBytes) && v<=c && memory.Covered(b,v,3)),"web_readlink");
                    break;
                case 221:
                    Need(file && b==0 && c==0 && d==2 && v==Bits(-38),"web_fadvise_unsupported");
                    break;
                default:
                    return false;
            }
            return true;
        }
    }
}
