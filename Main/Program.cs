using Compile;
using Interpret;
using Lex;
using Parse;
using Primitive;

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
// Console.WriteLine(IR);
ReadOnlySpan<byte> bytecode = Compiler.to_bytecode(IR);

// for (int i = 0; i < bytecode.Length; ++i){
    // Console.Write($"{bytecode[i].ToString().PadLeft(3)}, ");
    // if ((i + 1) % 10 == 0)
        // Console.WriteLine();
// }

string out_dir_name = "Omni_out";
string file_name = Path.GetFileName(argv[1]);

Directory.CreateDirectory(out_dir_name);
File.WriteAllLines($"{out_dir_name}/{file_name}.ir", [$"src: {Path.GetFullPath(argv[1])}{Environment.NewLine}", IR]);
File.WriteAllBytes($"{out_dir_name}/{file_name}.bc", bytecode);

Console.WriteLine($"\nreturn value: {Interpreter.run(bytecode, Value.get_argv(argv[2..]))}");

// for (Value.Binary_op op = Value.Binary_op.CMP_EQ; (int)op <= (int)Value.Binary_op.XOR; op = (Value.Binary_op)((int)op + 1)){
    // Console.WriteLine(op);
// 
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.BOOL, Value.Type_info.BOOL)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.BOOL, Value.Type_info.CHAR)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.BOOL, Value.Type_info.INT)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.BOOL, Value.Type_info.FLOAT)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.BOOL, Value.Type_info.STR)}");
// 
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.CHAR, Value.Type_info.BOOL)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.CHAR, Value.Type_info.CHAR)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.CHAR, Value.Type_info.INT)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.CHAR, Value.Type_info.FLOAT)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.CHAR, Value.Type_info.STR)}");
// 
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.INT, Value.Type_info.BOOL)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.INT, Value.Type_info.CHAR)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.INT, Value.Type_info.INT)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.INT, Value.Type_info.FLOAT)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.INT, Value.Type_info.STR)}");
// 
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.FLOAT, Value.Type_info.BOOL)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.FLOAT, Value.Type_info.CHAR)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.FLOAT, Value.Type_info.INT)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.FLOAT, Value.Type_info.FLOAT)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.FLOAT, Value.Type_info.STR)}");
// 
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.STR, Value.Type_info.BOOL)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.STR, Value.Type_info.CHAR)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.STR, Value.Type_info.INT)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.STR, Value.Type_info.FLOAT)}");
    // Console.WriteLine($"\t{op.get_result_type(Value.Type_info.STR, Value.Type_info.STR)}");
// }
