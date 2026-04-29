using Compile;
using Interpret;
using Lex;
using Parse;
using System.Text;

string[] argv = Environment.GetCommandLineArgs();

ReadOnlySpan<Token> tokens = Lexer.tokenize(argv[1]);
// foreach (Token tok in tokens)
    // Console.WriteLine(tok);
ReadOnlySpan<Node> AST = Parser.build_AST(tokens);
foreach (Node node in AST){
    Console.Write(node);
    Console.WriteLine(new string('-', 40));
}

string IR = Compiler.to_IR(AST);
Console.WriteLine(IR);
ReadOnlySpan<byte> bytecode = Compiler.to_bytecode(IR);

// for (int i = 0; i < bytecode.Length; ++i){
    // Console.Write($"{bytecode[i].ToString().PadLeft(3)}, ");
    // if ((i + 1) % 10 == 0)
        // Console.WriteLine();
// }
// 
string out_dir_name = "Omni_out";
string file_name = Path.GetFileName(argv[1]);

Directory.CreateDirectory(out_dir_name);
File.WriteAllLines($"{out_dir_name}/{file_name}.ir", [$"src: {Path.GetFullPath(argv[1])}{Environment.NewLine}", IR]);
File.WriteAllBytes($"{out_dir_name}/{file_name}.bc", bytecode);

Console.WriteLine($"return value: {Interpreter.run(bytecode, new Value(argv[2..].Select((s) => new StringBuilder(s)).ToArray()))}");
