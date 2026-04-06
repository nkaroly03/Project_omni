Lex.Lexer lexer = new("../fib.omni");

// foreach (Lex.Token tok in lexer.get_tokens())
    // Console.WriteLine(tok);

Parse.Parser parser = new Parse.Parser(lexer);
// foreach (Parse.Node node in parser.nodes)
    // Console.WriteLine(node);

Interpret.Interpreter interpreter = new(parser);
System.Console.WriteLine($"return {interpreter.run()}");
