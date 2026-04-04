namespace Parse;

public class Node{
    public required Node? lhs{ get; init; }
    public required Node? rhs{ get; init; }
    public required Lex.Token token{ get; init; }
}

public sealed class Parser{
    public Parser(List<Lex.Token> tokens){
        
    }
    public Parser(Lex.Lexer lexer) : this(lexer.get_tokens()){}

    /*

                                  let
                                   \
                                    \
                                     \
                                      =
                                     / \
                                    /   \
                                   /     \
                                  /       \
                                 /         \
                               float        +
                                           / \
                                          /   \
                                         /     \
                                        /       \
                                      3.14       *
                                                / \
                                               a   +
                                                  / \
                                                 /   \
                                                a     a

    */
}
