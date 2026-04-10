using Compile;
using Lex;
using Parse;

string[] argv = Environment.GetCommandLineArgs();

List<Token> tokens = Lexer.tokenize(argv[1]);
List<Node> AST = Parser.build_AST(tokens);

Interpret.AST_Interpreter interpreter = new(AST);

// foreach (Node node in AST)
    // Console.WriteLine(node);

// Console.WriteLine(new string('-', 20));

string IR = Compiler.to_IR(AST);
Console.WriteLine(IR);
Console.WriteLine(new string('-', 20));
foreach ((int i, byte instruction) in Compiler.to_bytecode(IR).Index()){
    Console.Write($"{instruction.ToString().PadLeft(3)}, ");
    if ((i + 1) % 10 == 0)
        Console.WriteLine();
}

// System.Console.WriteLine($"return {interpreter.run()}");

/*

    1 ; PUSH 10    
    2 ; PUSH 2     
    1 ; ADD        
    1 ; NEG        
    2 ; PUSH 4     
    2 ; NEG        
    1 ; MUL        
    1 ; TO_INT     

    2 ; PUSH 3.14  
    2 ; NEG        
    3 ; PUSH SP[-2]
    3 ; NEG        
    4 ; PUSH SP[-3]
    5 ; PUSH SP[-4]
    5 ; NEG        
    4 ; ADD        
    3 ; MUL
    2 ; ADD
    2 ; TO_FLOAT      

    2 ; PRINT "Hello" 

    3 ; PUSH SP[-1]   
    4 ; PUSH 3.25     
    3 ; ADD
    2 ; PRINT

    3 ; SCAN "Input: "
    4 ; PUSH 1        
    3 ; ADD
    3 ; TO_INT

    4 ; PUSH SP[-3]
    5 ; PUSH SP[-3]
    4 ; ADD
    3 ; MOV SP[-2]

    4 ; PUSH SP[-1]
    5 ; PUSH 10
    4 ; CMP_LE
    3 ; JMPZ 16
    4 ; PUSH FALSE
    4 ; NEG
    4 ; TO_BOOL
    4 ; PRINT "else if"
    5 ; PUSH SP[-2]
    6 ; PUSH 10
    5 ; CMP_EQ
    6 ; PUSH SP[-3]
    7 ; PUSH 10
    6 ; CMP_NEQ
    6 ; NEG
    5 ; CMP_EQ
    4 ; PRINT
    3 ; POP
    3 ; JMP 5
    4 ; PUSH 2.718
    4 ; TO_FLOAT
    4 ; PRINT "c44 == 10"
    3 ; POP

    4 ; PUSH TRUE
    3 ; JMPZ 2
    3 ; JMP -2

    2 ; RET
    1 ; POP
    0 ; POP

*/
