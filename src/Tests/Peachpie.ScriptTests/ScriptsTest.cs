using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Pchp.Core;
using Xunit;
using Xunit.Abstractions;

namespace ScriptsTest
{
    public class ScriptsTest
    {
        static readonly Context.IScriptingProvider _provider = Context.GlobalServices.GetService<Context.IScriptingProvider>(); // use IScriptingProvider singleton 

        private readonly ITestOutputHelper _output;

        public ScriptsTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [SkippableTheory]
        [ScriptsListData]
        public void ScriptRunTest(string dir, string fname)
        {
            var isSkipTest = new Regex(@"^skip(\([^)]*\))?_.*$"); // matches either skip_<smth>.php or skip(<reason>)_<smth>.php
            Skip.If(isSkipTest.IsMatch(fname));

            var path = Path.Combine(dir, fname);

            _output.WriteLine("Testing {0} ...", path);

            // test script compilation and run it
            var result = CompileAndRun(path);

            // invoke php.exe if possible and compare results
            var phpresult = result;

            try
            {
                phpresult = Interpret(path);
            }
            catch
            {
                _output.WriteLine("Running PHP failed.");
                return;
            }

            //
            Assert.Equal(phpresult, result);
        }


        string CompileAndRun(string path)
        {
            var outputStream = new MemoryStream();

            using (var ctx = Context.CreateEmpty())
            {
                // mimic the execution in the given folder
                ctx.WorkingDirectory = Path.GetDirectoryName(path);
                ctx.RootPath = ctx.WorkingDirectory;

                // redirect text output
                ctx.Output = new StreamWriter(outputStream, Encoding.UTF8) { AutoFlush = true };
                ctx.OutputStream = outputStream;

                // Compile and load 
                var script = _provider.CreateScript(new Context.ScriptOptions()
                {
                    Context = ctx,
                    IsSubmission = false,
                    EmitDebugInformation = false,
                    Location = new Location(path, 0, 0),
                }, File.ReadAllText(path));

                // run
                try
                {
                    script.Evaluate(ctx, ctx.Globals, null);
                }
                catch (ScriptDiedException ex)
                {
                    ex.ProcessStatus(ctx);
                }
            }

            //
            outputStream.Position = 0;
            return new StreamReader(outputStream, Encoding.UTF8).ReadToEnd();
        }

        static string Interpret(string path)
        {
            return RunProcess("php", Path.GetFileName(path), Path.GetDirectoryName(path));
        }

        static string RunProcess(string exe, string args, string cwd)
        {
            var process = new Process();
            process.StartInfo = new ProcessStartInfo(exe, args)
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                UseShellExecute = false,
                WorkingDirectory = cwd
            };

            //
            process.Start();

            // To avoid deadlocks, always read the output stream first and then wait.
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            //
            if (!string.IsNullOrEmpty(error))
                return error;

            //
            return output;
        }
    }
}
