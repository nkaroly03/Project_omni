Lex.Lexer l = new("../example.omni");

// foreach (Lex.Token tok in l.get_tokens())
    // Console.WriteLine(tok);

new Parse.Parser(l);
