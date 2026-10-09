using System;
using System.IO;
using System.Windows.Forms;

namespace PublisherToPdf
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            // Headless use (scripts, tests):
            //   PublisherToPdf.exe --idml <in.pub> <out.idml> [--tables-as-table]
            //   PublisherToPdf.exe --pdf  <in.pub> <out.pdf>
            // A WinExe has no console, so results go to <out>.log and the exit code.
            if (args.Length >= 3 && (args[0] == "--idml" || args[0] == "--pdf"))
                return Headless(args);
            //   PublisherToPdf.exe --batch-idml <outDir> <in1.pub> <in2.pub> ...   (one Publisher session, like the UI)
            if (args.Length >= 3 && args[0] == "--batch-idml")
                return HeadlessBatch(args);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }

        private static int HeadlessBatch(string[] args)
        {
            string outDir = args[1];
            Directory.CreateDirectory(outDir);
            string log = Path.Combine(outDir, "batch.log");
            var sb = new System.Text.StringBuilder();
            int failed = 0;
            int rounds = Array.IndexOf(args, "--twice") >= 0 ? 2 : 1;   // a second batch in the same process, as the UI does
            for (int round = 0; round < rounds; round++)
            using (var converter = new PublisherConverter())
            {
                converter.Start();
                bool both = Array.IndexOf(args, "--both") >= 0;   // PDF (fit to content) then IDML, as the UI's "Both" does
                for (int i = 2; i < args.Length; i++)
                {
                    string input = args[i];
                    if (input == "--both" || input == "--twice") continue;
                    string output = Path.Combine(outDir, Path.GetFileNameWithoutExtension(input) + ".idml");
                    try
                    {
                        if (both) converter.Convert(input, Path.ChangeExtension(output, ".pdf"), true);
                        var warnings = converter.ConvertToIdml(input, output, new Idml.IdmlOptions());
                        sb.Append("OK ").Append(output).Append(" (").Append(warnings.Count).Append(" notes)\r\n");
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        sb.Append("FAILED ").Append(input).Append("\r\n").Append(ex).Append("\r\n");
                    }
                }
            }
            sb.Append("[" + PublisherConverter.LastDisposeNote + "]\r\n").Append(PublisherConverter.Trace);
            File.WriteAllText(log, sb.ToString());
            return failed == 0 ? 0 : 1;
        }

        private static int Headless(string[] args)
        {
            string input = args[1], output = args[2];
            string log = output + ".log";
            try
            {
                using (var converter = new PublisherConverter())
                {
                    converter.Start();
                    if (args[0] == "--pdf")
                    {
                        converter.Convert(input, output, false);
                        File.WriteAllText(log, "OK " + output + "\r\n");
                    }
                    else
                    {
                        var options = new Idml.IdmlOptions();
                        string dumpModel = null;
                        foreach (string a in args)
                        {
                            if (a == "--tables-as-table") options.TablesAsFrames = false;
                            if (a == "--dump-model") dumpModel = output + ".model.json";   // the PubDocument as JSON, to diff against tools\Dump-PubObjectModel.ps1
                        }
                        var warnings = converter.ConvertToIdml(input, output, options, dumpModel);
                        File.WriteAllText(log, "OK " + output + "\r\n" + string.Join("\r\n", warnings) + "\r\n");
                    }
                }
                File.AppendAllText(log, "[" + PublisherConverter.LastDisposeNote + "]\r\n" + PublisherConverter.Trace);
                return 0;
            }
            catch (Exception ex)
            {
                File.WriteAllText(log, "FAILED " + ex + "\r\n");
                return 1;
            }
        }
    }
}
