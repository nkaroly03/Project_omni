namespace Interpret;

using Compile;
using System.Diagnostics;
using System.Text;

public static class Interpreter{
    extension(List<Value> self){
        Value pop(){
            Value temp = self[^1];

            self.RemoveAt(self.Count - 1);

            return temp;
        }
    }

    public static Value run(ReadOnlySpan<byte> bytecode, Value argv){
        if (argv.data is not StringBuilder[])
            throw new ArgumentOutOfRangeException("argv must contain an array of StringBuilders");

        List<Value> stack = new();
        Random rd = new();
        
        for (int pc = 0; pc < bytecode.Length;){
            switch ((Compiler.Op_code)bytecode[pc++]){
                case Compiler.Op_code.PUSH_FROM_SP:
                    stack.Add(new(stack[stack.Count + BitConverter.ToInt32(bytecode[pc..][..sizeof(int)])]));
                    pc += sizeof(int);
                    break;
                case Compiler.Op_code.PUSH_ARGC:
                    stack.Add(new(((StringBuilder[])argv.data).Length));
                    break;
                case Compiler.Op_code.PUSH_ARGV:
                    stack.Add(new(argv));
                    break;
                case Compiler.Op_code.PUSH_FALSE:
                    stack.Add(new(false));
                    break;
                case Compiler.Op_code.PUSH_TRUE:
                    stack.Add(new(true));
                    break;
                case Compiler.Op_code.PUSH_CHAR:
                    stack.Add(new(BitConverter.ToChar(bytecode[pc..][..sizeof(char)])));
                    pc += sizeof(char);
                    break;
                case Compiler.Op_code.PUSH_INT:
                    stack.Add(new(BitConverter.ToInt32(bytecode[pc..][..sizeof(int)])));
                    pc += sizeof(int);
                    break;
                case Compiler.Op_code.PUSH_FLOAT:
                    stack.Add(new(BitConverter.ToSingle(bytecode[pc..][..sizeof(float)])));
                    pc += sizeof(float);
                    break;
                case Compiler.Op_code.PUSH_STR:
                    int strlen = BitConverter.ToInt32(bytecode[pc..][..sizeof(int)]);
                    pc += sizeof(int);
                    stack.Add(new(new StringBuilder(Encoding.UTF8.GetString(bytecode[pc..][..strlen]))));
                    pc += strlen;
                    break;

                case Compiler.Op_code.POP:
                    stack.pop();
                    break;

                case Compiler.Op_code.RET:
                    pc = bytecode.Length;
                    break;

                case Compiler.Op_code.MOV:
                    int mov_value_idx = stack.Count + BitConverter.ToInt32(bytecode[pc..][..sizeof(int)]);
                    stack[mov_value_idx] = stack[mov_value_idx].data switch{
                        bool          => new(stack[^1].to_bool()),
                        char          => new(stack[^1].to_char()),
                        int           => new(stack[^1].to_int()),
                        float         => new(stack[^1].to_float()),
                        StringBuilder => new(stack[^1].to_string()),

                        bool[] or char[] or int[] or float[] or StringBuilder[] => throw new ArgumentOutOfRangeException("Trying to reassign an array"),

                        _ => throw new UnreachableException(),
                    };
                    pc += sizeof(int);
                    stack.pop();
                    break;
                case Compiler.Op_code.DEREF_MOV:
                    Value value_ref = stack[stack.Count + BitConverter.ToInt32(bytecode[pc..][..sizeof(int)])];
                    pc += sizeof(int);
                    value_ref[stack[^2]] = stack.pop();
                    stack.pop();
                    break;

                case Compiler.Op_code.JMP:
                    int jmp_count = BitConverter.ToInt32(bytecode[pc..][..sizeof(int)]);
                    pc += sizeof(int);
                    pc += (jmp_count + 1);
                    break;
                case Compiler.Op_code.JMPZ:
                    int jmpz_count = BitConverter.ToInt32(bytecode[pc..][..sizeof(int)]);
                    pc += sizeof(int);
                    pc += (Convert.ToInt32(!stack.pop().to_bool()) * (jmpz_count + 1));
                    break;

                case Compiler.Op_code.PRINT:
                    Console.Write(stack.pop());
                    break;

                case Compiler.Op_code.SCAN:
                    Console.Write(stack[^1]);
                    stack[^1] = new(new StringBuilder(Console.ReadLine()!));
                    break;

                case Compiler.Op_code.ARRAY_SIZE:
                    stack[^1] = stack[^1].data switch{
                        StringBuilder       sb => new(    sb.Length),
                        bool[]           b_arr => new( b_arr.Length),
                        char[]           c_arr => new( c_arr.Length),
                        int[]            i_arr => new( i_arr.Length),
                        float[]          f_arr => new( f_arr.Length),
                        StringBuilder[] sb_arr => new(sb_arr.Length),

                        _ => throw new ArgumentOutOfRangeException("Trying to get the size of a non-array type"),
                    };
                    break;
                case Compiler.Op_code.RAND:
                    stack.Add(new(rd.Next()));
                    break;
                case Compiler.Op_code.POLL_CHAR:
                    char polled_char = '\0';
                    if (Console.KeyAvailable){
                        polled_char = Console.ReadKey(true).KeyChar;
                        while (Console.KeyAvailable)
                            _ = Console.ReadKey(true);
                    }
                    stack.Add(new(polled_char));
                    break;

                case Compiler.Op_code.TO_BOOL:  stack[^1] = new(stack[^1].to_bool());   break;
                case Compiler.Op_code.TO_CHAR:  stack[^1] = new(stack[^1].to_char());   break;
                case Compiler.Op_code.TO_INT:   stack[^1] = new(stack[^1].to_int());    break;
                case Compiler.Op_code.TO_FLOAT: stack[^1] = new(stack[^1].to_float());  break;
                case Compiler.Op_code.TO_STR:   stack[^1] = new(stack[^1].to_string()); break;

                case Compiler.Op_code.ALLOC_ARRAY:
                    if (stack[^1].data is not (bool or char or int))
                        throw new ArgumentOutOfRangeException("Trying to allocate an array of non-integer size");
                    int alloc_size = stack[^1].to_int();
                    stack[^1] = (Compiler.Op_code)bytecode[pc++] switch{
                        Compiler.Op_code.TO_BOOL  => new(new bool [alloc_size]),
                        Compiler.Op_code.TO_CHAR  => new(new char [alloc_size]),
                        Compiler.Op_code.TO_INT   => new(new int  [alloc_size]),
                        Compiler.Op_code.TO_FLOAT => new(new float[alloc_size]),
                        Compiler.Op_code.TO_STR   => new(((Func<StringBuilder[]>)(() => {
                            StringBuilder[] sb_arr = new StringBuilder[alloc_size];
                            for (int i = 0; i < sb_arr.Length; ++i)
                                sb_arr[i] = new();
                            return sb_arr;
                        }))()),

                        _ => throw new UnreachableException(),
                    };
                    break;

                case Compiler.Op_code.CMP_EQ:  stack[^2] = new(stack[^2] == stack.pop()); break;
                case Compiler.Op_code.CMP_NEQ: stack[^2] = new(stack[^2] != stack.pop()); break;
                case Compiler.Op_code.CMP_LE:  stack[^2] = new(stack[^2] <  stack.pop()); break;
                case Compiler.Op_code.CMP_LEQ: stack[^2] = new(stack[^2] <= stack.pop()); break;
                case Compiler.Op_code.CMP_GE:  stack[^2] = new(stack[^2] >  stack.pop()); break;
                case Compiler.Op_code.CMP_GEQ: stack[^2] = new(stack[^2] >= stack.pop()); break;

                case Compiler.Op_code.ADD:   stack[^2]  += stack.pop(); break;
                case Compiler.Op_code.SUB:   stack[^2]  -= stack.pop(); break;
                case Compiler.Op_code.MUL:   stack[^2]  *= stack.pop(); break;
                case Compiler.Op_code.DIV:   stack[^2]  /= stack.pop(); break;
                case Compiler.Op_code.MOD:   stack[^2]  %= stack.pop(); break;
                case Compiler.Op_code.SHL:   stack[^2] <<= stack.pop(); break;
                case Compiler.Op_code.SHR:   stack[^2] >>= stack.pop(); break;
                case Compiler.Op_code.BAND:  stack[^2]  &= stack.pop(); break;
                case Compiler.Op_code.BOR:   stack[^2]  |= stack.pop(); break;
                case Compiler.Op_code.XOR:   stack[^2]  ^= stack.pop(); break;

                case Compiler.Op_code.DEREF:
                    stack[^2] = stack[^2][stack.pop()];
                    break;
                                            
                case Compiler.Op_code.POW:
                    stack[^2] = Value.pow(stack[^2], stack.pop());
                    break;

                case Compiler.Op_code.BNEG:
                    stack[^1] = ~stack[^1];
                    break;

                // case Compiler.Op_code.AND:
                    // stack[^2] = new(stack[^2].to_bool() && stack[^1].to_bool());
                    // stack.pop();
                    // break;
                // case Compiler.Op_code.OR:
                    // stack[^2] = new(stack[^2].to_bool() || stack[^1].to_bool());
                    // stack.pop();
                    // break;
                case Compiler.Op_code.NEG:
                    stack[^1] = -stack[^1];
                    break;
            }
        }

        return stack[^1];
    }
    public static Value run(ReadOnlySpan<byte> bytecode) => run(bytecode, new(new StringBuilder[0]));
}
