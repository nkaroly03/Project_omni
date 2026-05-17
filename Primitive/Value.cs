namespace Primitive;

using System.Diagnostics;
using System.Text;

public static class Primitive_extensions{
    extension(Type_info self){
        public bool  bool_is_set() => (self & Type_info.BOOL)  == Type_info.BOOL;
        public bool  char_is_set() => (self & Type_info.CHAR)  == Type_info.CHAR;
        public bool   int_is_set() => (self & Type_info.INT)   == Type_info.INT;
        public bool float_is_set() => (self & Type_info.FLOAT) == Type_info.FLOAT;
        public bool   str_is_set() => (self & Type_info.STR)   == Type_info.STR;
        public bool array_is_set() => (self & Type_info.ARRAY) == Type_info.ARRAY;

        public bool int_like_is_set() => self.bool_is_set() || self.char_is_set() || self.int_is_set();

        public string get_str_repr(){
            string s = "";
            if (self.array_is_set())
                s += "[]";
            s += (self & ~Type_info.ARRAY).ToString().ToLower();
            return s;
        }
    }

    extension(Unary_op self){
        public bool is_logical() => self == Unary_op.NOT;
        public bool is_bitwise() => self == Unary_op.BNEG;

        public bool is_valid_op(Type_info val) => self switch{
            Unary_op.PLUS or Unary_op.MINUS or Unary_op.NOT => !val.array_is_set() && (val.int_like_is_set() || val.float_is_set()),
            Unary_op.BNEG                                   => !val.array_is_set() && val.int_like_is_set(),

            _ => throw new UnreachableException(),
        };

        public Type_info get_result_type(Type_info val) => (self.is_valid_op(val)) ? ((self.is_logical()) ? Type_info.BOOL : val) : Type_info.INVALID;
    }
    extension(Binary_op self){
        public bool is_comparison() => self >= Binary_op.CMP_EQ && self <= Binary_op.CMP_GEQ;
        public bool is_bitwise() => self >= Binary_op.SHL && self <= Binary_op.XOR;
        public bool is_logical() => self == Binary_op.AND || self == Binary_op.OR;

        public bool is_valid_op(Type_info lhs, Type_info rhs){
            if (rhs.array_is_set())
                return false;

            if (self == Binary_op.SUBSCRIPT)
                return ((lhs.array_is_set() || lhs.str_is_set()) && rhs.int_like_is_set());

            if (lhs.array_is_set())
                return false;

            if (self == Binary_op.ASSIGNMENT)
                return true;

            if (lhs.str_is_set() || rhs.str_is_set()){
                return !self.is_logical() && (
                    (self == Binary_op.ADD && (lhs.char_is_set() || lhs.str_is_set()) && (rhs.char_is_set() || rhs.str_is_set())) ||
                    (self.is_comparison() && lhs.str_is_set() && rhs.str_is_set())
                );
            }

            if (self.is_bitwise())
                return lhs.int_like_is_set() && rhs.int_like_is_set();

            return true;
        }

        public Type_info get_result_type(Type_info lhs, Type_info rhs){
            if (!self.is_valid_op(lhs, rhs))
                return Type_info.INVALID;

            if (self.is_comparison() || self.is_logical())
                return Type_info.BOOL;

            if (self == Binary_op.SUBSCRIPT)
                return (lhs.array_is_set()) ? lhs & ~Type_info.ARRAY : Type_info.CHAR;

            if (self == Binary_op.ASSIGNMENT)
                return lhs;

            return lhs switch{
                Type_info.BOOL                   => rhs,
                Type_info.CHAR                   => (rhs == Type_info.BOOL) ? lhs : rhs,
                Type_info.INT                    => (rhs == Type_info.BOOL || rhs == Type_info.CHAR) ? lhs : rhs,
                Type_info.FLOAT or Type_info.STR => lhs,

                _ => throw new UnreachableException(),
            };
        }
    }
}

[Flags] public enum Type_info{
    INVALID = 0,
    BOOL    = 1,
    CHAR    = 2,
    INT     = 4,
    FLOAT   = 8,
    STR     = 16,
    ARRAY   = 32
}

public enum Unary_op{ PLUS, MINUS, BNEG, NOT }
public enum Binary_op{
    CMP_EQ,
    CMP_NEQ,
    CMP_LE,
    CMP_LEQ,
    CMP_GE,
    CMP_GEQ,

    SUBSCRIPT,

    ASSIGNMENT,

    ADD,
    SUB,
    MUL,
    DIV,
    MOD,
    POW,

    SHL,
    SHR,
    BAND,
    BOR,
    XOR,

    AND,
    OR
}

public sealed class Value : IEquatable<Value>, IComparable<Value>{
    static Value arithm_op(Value v1, Value v2, Action<Value, Value> op){
        if (v1.data.GetType().IsArray || v2.data.GetType().IsArray)
            throw new ArgumentOutOfRangeException("Trying to use arithmetic operation on array(s)");

        v1 = new(v1);
        v2 = new(v2);

        v1.data = v1.data switch{
            bool b => v2.data switch{
                bool  => v1.data,
                char  => (char)Convert.ToInt32(b),
                int   => Convert.ToInt32(b),
                float => Convert.ToSingle(b),

                _ => throw new ArgumentOutOfRangeException("Trying to use arithmetic operation on bool with string"),
            },
            char c => v2.data switch{
                bool or char  => v1.data,
                int           => (int)c,
                float         => (float)c,
                StringBuilder => new StringBuilder(c.ToString()),

                _ => throw new UnreachableException(),
            },
            int i => v2.data switch{
                bool or char or int => v1.data,
                float               => (float)i,

                _ => throw new ArgumentOutOfRangeException("Trying to use arithmetic operation on int with string"),
            },
            float => v2.data switch{
                bool or char or int or float => v1.data,

                _ => throw new ArgumentOutOfRangeException("Trying to use arithmetic operation on float with string"),
            },
            StringBuilder => v2.data switch{
                char or StringBuilder => v1.data,

                _ => throw new ArgumentOutOfRangeException("Trying to use arithmetic operation on string with bool, int or float"),
            },

            _ => throw new UnreachableException(),
        };

        op(v1, v2);

        return v1;
    }

    public static Value operator+(Value v) => v.data switch{
        bool          b => new(b),
        char          c => new(c),
        int           i => new(i),
        float         f => new(f),
        StringBuilder   => throw new ArgumentOutOfRangeException("Trying to use unary + on string"),

        bool[] or char[] or int[] or float[] or StringBuilder[] => throw new ArgumentOutOfRangeException("Trying to use unary + on array"),

        _ => throw new UnreachableException(),
    };
    public static Value operator-(Value v) => v.data switch{
        bool          b => new(!b), // TODO: correct logic?
        char          c => new((char)-c),
        int           i => new(-i),
        float         f => new(-f),
        StringBuilder   => throw new ArgumentOutOfRangeException("Trying to use unary - on string"),

        bool[] or char[] or int[] or float[] or StringBuilder[] => throw new ArgumentOutOfRangeException("Trying to use unary - on array"),

        _ => throw new UnreachableException(),
    };
    public static Value operator~(Value v) => v.data switch{
        bool          b => new(!b),
        char          c => new((char)~c),
        int           i => new(~i),
        float           => throw new ArgumentOutOfRangeException("Trying to use bitwise negation on float"),
        StringBuilder   => throw new ArgumentOutOfRangeException("Trying to use bitwise negation on string"),

        bool[] or char[] or int[] or float[] or StringBuilder[] => throw new ArgumentOutOfRangeException("Trying to use bitwise negation on array"),

        _ => throw new UnreachableException(),
    };

    public static Value operator+(Value v1, Value v2) => arithm_op(v1, v2, (_v1, _v2) => _v1.add_eq(_v2));
    public static Value operator-(Value v1, Value v2) => arithm_op(v1, v2, (_v1, _v2) => _v1.sub_eq(_v2));
    public static Value operator*(Value v1, Value v2) => arithm_op(v1, v2, (_v1, _v2) => _v1.mul_eq(_v2));
    public static Value operator/(Value v1, Value v2) => arithm_op(v1, v2, (_v1, _v2) => _v1.div_eq(_v2));
    public static Value operator%(Value v1, Value v2) => arithm_op(v1, v2, (_v1, _v2) => _v1.mod_eq(_v2));
    public static Value       pow(Value v1, Value v2) => arithm_op(v1, v2, (_v1, _v2) => _v1.pow_eq(_v2));

    public static Value operator<<(Value v1, Value v2) => arithm_op(v1, v2, (_v1, _v2) => _v1. shl_eq(_v2));
    public static Value operator>>(Value v1, Value v2) => arithm_op(v1, v2, (_v1, _v2) => _v1. shr_eq(_v2));
    public static Value operator& (Value v1, Value v2) => arithm_op(v1, v2, (_v1, _v2) => _v1.band_eq(_v2));
    public static Value operator| (Value v1, Value v2) => arithm_op(v1, v2, (_v1, _v2) => _v1. bor_eq(_v2));
    public static Value operator^ (Value v1, Value v2) => arithm_op(v1, v2, (_v1, _v2) => _v1. xor_eq(_v2));

    public static bool operator==(Value v1, Value v2) =>  v1.Equals(v2);
    public static bool operator!=(Value v1, Value v2) => !v1.Equals(v2);
    public static bool operator< (Value v1, Value v2) =>  v1.CompareTo(v2) <  0;
    public static bool operator<=(Value v1, Value v2) =>  v1.CompareTo(v2) <= 0;
    public static bool operator> (Value v1, Value v2) =>  v1.CompareTo(v2) >  0;
    public static bool operator>=(Value v1, Value v2) =>  v1.CompareTo(v2) >= 0;

    public static Value get_argv(IEnumerable<string> argv) => new(argv.Select((s) => new StringBuilder(s)).ToArray());

    public object data{ get; private set; }

    public Value(bool data)            => this.data = data;
    public Value(char data)            => this.data = data;
    public Value(int data)             => this.data = data;
    public Value(float data)           => this.data = data;
    public Value(StringBuilder data)   => this.data = data;

    public Value(bool[] data)          => this.data = data;
    public Value(char[] data)          => this.data = data;
    public Value(int[] data)           => this.data = data;
    public Value(float[] data)         => this.data = data;
    public Value(StringBuilder[] data) => this.data = data;

    public Value(Value other) => data = other.data switch{
        bool   or char   or int   or float   or
        bool[] or char[] or int[] or float[] or StringBuilder[] => other.data,
        StringBuilder sb => new StringBuilder(sb.ToString()),

        _ => throw new UnreachableException(),
    };

    public Value this[Value val]{
        get{
            if (val.data.GetType().IsArray)
                throw new ArgumentOutOfRangeException("Trying to use indexer with an array as an index");
            if (val.data is StringBuilder)
                throw new ArgumentOutOfRangeException("Trying to use indexer with a string as an index");
            if (val.data is float)
                throw new ArgumentOutOfRangeException("Trying to use indexer with a float as an index");
            int i = val.to_int();
            return data switch{
                StringBuilder sb => new(sb[i]),

                bool[]           b_arr => new( b_arr[i]),
                char[]           c_arr => new( c_arr[i]),
                int[]            i_arr => new( i_arr[i]),
                float[]          f_arr => new( f_arr[i]),
                StringBuilder[] sb_arr => new(sb_arr[i]),

                _ => throw new ArgumentOutOfRangeException("Trying to use indexer on non array-like type"),
            };
        }
        set{
            if (val.data.GetType().IsArray)
                throw new ArgumentOutOfRangeException("Trying to use indexer with an array as an index");
            if (val.data is StringBuilder)
                throw new ArgumentOutOfRangeException("Trying to use indexer with a string as an index");
            if (val.data is float)
                throw new ArgumentOutOfRangeException("Trying to use indexer with a float as an index");
            int i = val.to_int();
            switch (data){
                case StringBuilder       sb:     sb[i] = value.to_char();   break;
                case bool[]           b_arr:  b_arr[i] = value.to_bool();   break;
                case char[]           c_arr:  c_arr[i] = value.to_char();   break;
                case int[]            i_arr:  i_arr[i] = value.to_int();    break;
                case float[]          f_arr:  f_arr[i] = value.to_float();  break;
                case StringBuilder[] sb_arr: sb_arr[i] = value.to_string(); break;

                default: throw new ArgumentOutOfRangeException("Trying to use indexer on non array-like type");
            }
        }
    }

    public override bool Equals(object? obj) => Equals(obj as Value);
    public override int GetHashCode() => data.GetHashCode();
    public override string ToString(){
        if (data.GetType().IsArray){
            StringBuilder result = new();
            result.Append('[');
            switch (data){
                case bool[] b_arr:
                    foreach (bool b in b_arr)
                        result.Append($"{b}, ");
                    break;
                case char[] c_arr:
                    foreach (char c in c_arr)
                        result.Append($"'{c}', ");
                    break;
                case int[] i_arr:
                    foreach (int i in i_arr)
                        result.Append($"{i}, ");
                    break;
                case float[] f_arr:
                    foreach (float f in f_arr)
                        result.Append($"{f}, ");
                    break;
                case StringBuilder[] sb_arr:
                    foreach (StringBuilder sb in sb_arr)
                        result.Append($"\"{sb.ToString()}\", ");
                    break;
                default:
                    throw new UnreachableException();
            }
            result.Length -= (Convert.ToInt32(result[^1] == ' ') * 2);
            result.Append(']');
            return result.ToString();
        }
        return data.ToString()!;
    }

    public bool Equals(Value? other){
        if (other is null)
            return false;

        if (data.GetType().IsArray || other.data.GetType().IsArray)
            throw new ArgumentOutOfRangeException("Trying to compare an array to an array");

        if (data is StringBuilder || other.data is StringBuilder){
            if (data is StringBuilder sb1 && other.data is StringBuilder sb2)
                return sb1.ToString().Equals(sb2.ToString());
            throw new ArgumentOutOfRangeException("Trying to compare a string to a non-string");
        }

        return (data is float || other.data is float) ? to_float().Equals(other.to_float()) : to_int().Equals(other.to_int());
    }
    public int CompareTo(Value? other){
        if (other is null)
            return 1;

        if (data.GetType().IsArray || other.data.GetType().IsArray)
            throw new ArgumentOutOfRangeException("Trying to compare an array to an array");

        if (data is StringBuilder || other.data is StringBuilder){
            if (data is StringBuilder sb1 && other.data is StringBuilder sb2)
                return string.Compare(sb1.ToString(), sb2.ToString(), StringComparison.Ordinal);
            throw new ArgumentOutOfRangeException("Trying to compare a string to a non-string");
        }

        return (data is float || other.data is float) ? to_float().CompareTo(other.to_float()) : to_int().CompareTo(other.to_int());
    }

    public bool to_bool() => data switch{
        bool           b => b,
        char           c => Convert.ToBoolean((int)c),
        int            i => Convert.ToBoolean(i),
        float          f => Convert.ToBoolean(f),
        StringBuilder sb => bool.Parse(sb.ToString()),

        bool[] or char[] or int[] or float[] or StringBuilder[] => throw new ArgumentOutOfRangeException("Trying to convert an array to bool"),

        _ => throw new UnreachableException(),
    };
    public char to_char() => data switch{
        bool           b => (char)Convert.ToInt32(b),
        char           c => c,
        int            i => (char)i,
        float          f => (char)f,
        StringBuilder sb => char.Parse(sb.ToString()),

        bool[] or char[] or int[] or float[] or StringBuilder[] => throw new ArgumentOutOfRangeException("Trying to convert an array to char"),

        _ => throw new UnreachableException(),
    };
    public int to_int() => data switch{
        bool           b => Convert.ToInt32(b),
        char           c => (int)c,
        int            i => i,
        float          f => (int)f,
        StringBuilder sb => int.Parse(sb.ToString()),

        bool[] or char[] or int[] or float[] or StringBuilder[] => throw new ArgumentOutOfRangeException("Trying to convert an array to int"),

        _ => throw new UnreachableException(),
    };
    public float to_float() => data switch{
        bool           b => Convert.ToSingle(b),
        char           c => (float)c,
        int            i => (float)i,
        float          f => f,
        StringBuilder sb => float.Parse(sb.ToString(), System.Globalization.CultureInfo.InvariantCulture),

        bool[] or char[] or int[] or float[] or StringBuilder[] => throw new ArgumentOutOfRangeException("Trying to convert an array to float"),

        _ => throw new UnreachableException(),
    };
    public StringBuilder to_string() => data switch{
        bool           b => new( b.ToString()),
        char           c => new( c.ToString()),
        int            i => new( i.ToString()),
        float          f => new( f.ToString()),
        StringBuilder sb => new(sb.ToString()),

        bool[] or char[] or int[] or float[] or StringBuilder[] => throw new ArgumentOutOfRangeException("Trying to convert an array to string"),

        _ => throw new UnreachableException(),
    };

    public void add_eq(Value other){
        if (data.GetType().IsArray || other.data.GetType().IsArray)
            throw new ArgumentOutOfRangeException("Trying to do addition with array(s)");

        if (data is not StringBuilder && other.data is StringBuilder)
            throw new ArgumentOutOfRangeException("Trying to do addition on a non-string with a string");

        data = data switch{
            bool           b => Convert.ToBoolean((Convert.ToInt32(b) + Convert.ToInt32(other.to_bool())) & 1),
            char           c => (char)(c + other.to_char()),
            int            i => i + other.to_int(),
            float          f => f + other.to_float(),
            StringBuilder sb => other.data switch{
                char           c_other => sb.Append( c_other),
                StringBuilder sb_other => sb.Append(sb_other.ToString()),

                _ => throw new ArgumentOutOfRangeException("Trying to do addition on a string with bool, int or float"),
            },

            _ => throw new UnreachableException()
        };
    }
    public void sub_eq(Value other) => data = data switch{
        bool          b => Convert.ToBoolean(Convert.ToInt32(b) - Convert.ToInt32(other.to_bool())),
        char          c => (char)(c - other.to_char()),
        int           i => i - other.to_int(),
        float         f => f - other.to_float(),
        StringBuilder   => throw new ArgumentOutOfRangeException("Trying to do subtraction with string(s)"),

        bool[] or char[] or int[] or float[] or StringBuilder[] => throw new ArgumentOutOfRangeException("Trying to do subtraction with array(s)"),

        _ => throw new UnreachableException(),
    };
    public void mul_eq(Value other) => data = data switch{
        bool          b => Convert.ToBoolean(Convert.ToInt32(b) * Convert.ToInt32(other.to_bool())),
        char          c => (char)(c * other.to_char()),
        int           i => i * other.to_int(),
        float         f => f * other.to_float(),
        StringBuilder   => throw new ArgumentOutOfRangeException("Trying to do multiplication with string(s)"),

        bool[] or char[] or int[] or float[] or StringBuilder[] => throw new ArgumentOutOfRangeException("Trying to do multiplication with array(s)"),

        _ => throw new UnreachableException(),
    };
    public void div_eq(Value other) => data = data switch{
        bool          b => Convert.ToBoolean(Convert.ToInt32(b) / Convert.ToInt32(other.to_bool())),
        char          c => (char)(c / other.to_char()),
        int           i => i / other.to_int(),
        float         f => f / other.to_float(),
        StringBuilder   => throw new ArgumentOutOfRangeException("Trying to do division with string(s)"),

        bool[] or char[] or int[] or float[] or StringBuilder[] => throw new ArgumentOutOfRangeException("Trying to do division with array(s)"),

        _ => throw new UnreachableException(),
    };
    public void mod_eq(Value other) => data = data switch{
        bool          b => Convert.ToBoolean(Convert.ToInt32(b) % Convert.ToInt32(other.to_bool())),
        char          c => (char)(c % other.to_char()),
        int           i => i % other.to_int(),
        float         f => f % other.to_float(),
        StringBuilder   => throw new ArgumentOutOfRangeException("Trying to do modulo with string(s)"),

        bool[] or char[] or int[] or float[] or StringBuilder[] => throw new ArgumentOutOfRangeException("Trying to do modulo with array(s)"),

        _ => throw new UnreachableException(),
    };
    public void pow_eq(Value other){
        if (data.GetType().IsArray || other.data.GetType().IsArray)
            throw new ArgumentOutOfRangeException("Trying to do exponentiation with array(s)");
        if (data is StringBuilder || other.data is StringBuilder)
            throw new ArgumentOutOfRangeException("Trying to do exponentiation string(s)");

        float result = MathF.Pow(to_float(), other.to_float());
        data = data switch{
            bool  => Convert.ToBoolean(result),
            char  => (char)result,
            int   => (int)result,
            float => result,

            _ => throw new UnreachableException()
        };
    }

    public void shl_eq(Value other){
        if (data.GetType().IsArray || other.data.GetType().IsArray)
            throw new ArgumentOutOfRangeException("Trying to do bitwise left-shift with array(s)");
        if (data is StringBuilder || other.data is StringBuilder)
            throw new ArgumentOutOfRangeException("Trying to do bitwise left-shift with string(s)");
        if (data is float || other.data is float)
            throw new ArgumentOutOfRangeException("Trying to do bitwise left-shift with float(s)");

        data = data switch{
            bool b => b,
            char c => (char)(c << other.to_char()),
            int  i => i << other.to_int(),

            _ => throw new UnreachableException(),
        };
    }
    public void shr_eq(Value other){
        if (data.GetType().IsArray || other.data.GetType().IsArray)
            throw new ArgumentOutOfRangeException("Trying to do bitwise right-shift with array(s)");
        if (data is StringBuilder || other.data is StringBuilder)
            throw new ArgumentOutOfRangeException("Trying to do bitwise right-shift with string(s)");
        if (data is float || other.data is float)
            throw new ArgumentOutOfRangeException("Trying to do bitwise right-shift with float(s)");

        data = data switch{
            bool b => b,
            char c => (char)(c >> other.to_char()),
            int  i => i >> other.to_int(),

            _ => throw new UnreachableException(),
        };
    }
    public void band_eq(Value other){
        if (data.GetType().IsArray || other.data.GetType().IsArray)
            throw new ArgumentOutOfRangeException("Trying to do bitwise and with array(s)");
        if (data is StringBuilder || other.data is StringBuilder)
            throw new ArgumentOutOfRangeException("Trying to do bitwise and with string(s)");
        if (data is float || other.data is float)
            throw new ArgumentOutOfRangeException("Trying to do bitwise and with float(s)");

        data = data switch{
            bool b => b && other.to_bool(),
            char c => (char)(c & other.to_char()),
            int  i => i & other.to_int(),

            _ => throw new UnreachableException(),
        };
    }
    public void bor_eq(Value other){
        if (data.GetType().IsArray || other.data.GetType().IsArray)
            throw new ArgumentOutOfRangeException("Trying to do bitwise or with array(s)");
        if (data is StringBuilder || other.data is StringBuilder)
            throw new ArgumentOutOfRangeException("Trying to do bitwise or with string(s)");
        if (data is float || other.data is float)
            throw new ArgumentOutOfRangeException("Trying to do bitwise or with float(s)");

        data = data switch{
            bool b => b || other.to_bool(),
            char c => (char)(c | other.to_char()),
            int  i => i | other.to_int(),

            _ => throw new UnreachableException(),
        };
    }
    public void xor_eq(Value other){
        if (data.GetType().IsArray || other.data.GetType().IsArray)
            throw new ArgumentOutOfRangeException("Trying to do xor with array(s)");
        if (data is StringBuilder || other.data is StringBuilder)
            throw new ArgumentOutOfRangeException("Trying to do xor with string(s)");
        if (data is float || other.data is float)
            throw new ArgumentOutOfRangeException("Trying to do xor with float(s)");

        data = data switch{
            bool b => b != other.to_bool(),
            char c => (char)(c ^ other.to_char()),
            int  i => i ^ other.to_int(),

            _ => throw new UnreachableException(),
        };
    }
}
