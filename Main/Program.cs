using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Compile;
using Interpret;
using Lex;
using Parse;
class Program
{
    static void Main(string[] args)
    {
        var db = new DatabaseManager();
        db.InitializeDatabase();

        string targetFolder = "Examples";
        if (!Directory.Exists(targetFolder))
        {
            Console.WriteLine($"No folder name: {targetFolder}");
            return;
        }

        string[] allTestFiles = Directory.GetFiles(targetFolder, "*.omni");

        Console.WriteLine("\n\tAvailable Tests");
        Console.WriteLine("-----------------------------");
        for (int i = 0; i < allTestFiles.Length; i++)
        {
            Console.WriteLine($"[{i + 1}] {Path.GetFileName(allTestFiles[i])}");
        }
        Console.WriteLine($"[{allTestFiles.Length + 1}] Run all tests");

        Console.Write("\nSelect the tests you want to run (pl. 1, 3, 4) or choose the 'all' option: ");
        string? selectionInput = Console.ReadLine();

        List<string> selectedFiles = new List<string>();

        if (string.IsNullOrWhiteSpace(selectionInput) || selectionInput.Trim() == (allTestFiles.Length + 1).ToString())
        {
            selectedFiles.AddRange(allTestFiles);
        }
        else
        {
            var parts = selectionInput.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (int.TryParse(part, out int index) && index >= 1 && index <= allTestFiles.Length)
                {
                    selectedFiles.Add(allTestFiles[index - 1]);
                }
            }
        }

        if (selectedFiles.Count == 0)
        {
            Console.WriteLine("You haven't selected a valid test.");
            return;
        }

        Console.Write("How many threads should the test run on? (Press Enter to set the automatic maximum): ");
        string? threadInput = Console.ReadLine();

        int maxThreads = Environment.ProcessorCount;
        if (int.TryParse(threadInput, out int parsedThreads) && parsedThreads > 0)
        {
            maxThreads = parsedThreads;
        }

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxThreads
        };

        var successfulResults = new ConcurrentBag<RunResult>();
        var failedResults = new ConcurrentBag<RunResult>();

        Parallel.ForEach(selectedFiles, parallelOptions, filePath =>
        {
            var stopwatch = Stopwatch.StartNew();
            var result = ExecuteMiniVM(filePath);
            stopwatch.Stop();

            result.DurationMs = stopwatch.ElapsedMilliseconds;

            if (result.IsSuccess)
            {
                Console.WriteLine($"[Success] {result.ProgramName} running; time: ({result.DurationMs} ms)");
                Console.ResetColor();

                successfulResults.Add(result);
            }
            else
            {
                Console.WriteLine($"[Failed] {result.ProgramName} error: {result.ErrorCategory} - {result.ErrorMessage}");
                Console.ResetColor();

                failedResults.Add(result);
            }
        });

        db.SaveSuccessfulResults(new List<RunResult>(successfulResults));
        db.SaveFailedResults(new List<RunResult>(failedResults));

        db.PrintErrorStatistics();
    }

    static RunResult ExecuteMiniVM(string filePath)
    {
        string fileName = Path.GetFileName(filePath);
        string sourceCode = File.ReadAllText(filePath);

        var result = new RunResult { ProgramName = fileName, SourceCode = sourceCode };

        try
        {
            ReadOnlySpan<Token> tokens = Lexer.tokenize(filePath);
            ReadOnlySpan<Node> ast = Parser.build_AST(tokens);

            string ir = Compiler.to_IR(ast);
            result.RunLog = ir;

            ReadOnlySpan<byte> bytecodeSpan = Compiler.to_bytecode(ir);
            result.CompiledBytecode = bytecodeSpan.ToArray();

            Interpreter.run(bytecodeSpan);

            result.IsSuccess = true;
        }
        catch (Syntax_error_exception ex)
        {
            result.IsSuccess = false;
            result.ErrorCategory = "SyntaxError";
            result.ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorCategory = "RuntimeError";
            result.ErrorMessage = ex.Message;
        }

        return result;
    }
}
