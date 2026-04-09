using Compile;
using Lex;
using Parse;

string[] argv = Environment.GetCommandLineArgs();

List<Token> tokens = Lexer.tokenize(argv[1]);
List<Node> AST = Parser.build_AST(tokens);

Interpret.AST_Interpreter interpreter = new(AST);

foreach (Node node in AST)
    Console.WriteLine(node);

Console.WriteLine(new string('-', 20));

Console.WriteLine(Compiler.to_IR(AST));

// System.Console.WriteLine($"return {interpreter.run()}");
