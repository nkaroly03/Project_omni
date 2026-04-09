namespace Interpret;

public sealed class Interpreter{
    List<Value> m_stack;
    int m_pc;

    public Interpreter(byte[] bytecode) => (m_stack, m_pc) = (new(), new());

    // public Value run(){
    // }
}
