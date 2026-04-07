string[] argv = Environment.GetCommandLineArgs();

Lex.Lexer lexer = new(argv[1]);
Parse.Parser parser = new Parse.Parser(lexer);
Interpret.AST_Interpreter interpreter = new(parser);

foreach (Parse.Node node in parser.nodes)
    Console.WriteLine(node);

// System.Console.WriteLine($"return {interpreter.run()}");
