namespace Interpret;

public sealed class BC_Interpreter{
    List<int> m_stack = new(9);
    int m_pc = 0;

    public BC_Interpreter(byte[] bytecode){
    }
}
