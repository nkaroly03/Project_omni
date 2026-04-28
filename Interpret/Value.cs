namespace Interpret;

using System.Diagnostics;
using System.Text;

public sealed class Value : IEquatable<Value>, IComparable<Value>{
    static Value arithm_op(Value v1, Value v2, Action<Value, Value> op){
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
    static Value barithm_op(Value v1, Value v2, Action<Value, Value> op){
        op(v1, v2);

        return v1;
    }

    public static Value operator+(Value v) => (v.data is not StringBuilder) ? new(v) : throw new ArgumentOutOfRangeException("Trying to use unary + on string");
    public static Value operator-(Value v) => v.data switch{
        bool  b => new(!b),
        char  c => new(-c),
        int   i => new(-i),
        float f => new(-f),

        _ => throw new ArgumentOutOfRangeException("Trying to use unary - on string"),
    };

    public static Value operator+(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.add_eq(_v2));
    public static Value operator-(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.sub_eq(_v2));
    public static Value operator*(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.mul_eq(_v2));
    public static Value operator/(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.div_eq(_v2));
    public static Value operator%(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.mod_eq(_v2));
    public static Value       pow(Value v1, Value v2) => arithm_op(new(v1), new(v2), (_v1, _v2) => _v1.pow_eq(_v2));

    public static Value operator<<(Value v1, Value v2) => barithm_op(new(v1), new(v2), (_v1, _v2) => _v1. shl_eq(_v2));
    public static Value operator>>(Value v1, Value v2) => barithm_op(new(v1), new(v2), (_v1, _v2) => _v1. shr_eq(_v2));
    public static Value operator& (Value v1, Value v2) => barithm_op(new(v1), new(v2), (_v1, _v2) => _v1.band_eq(_v2));
    public static Value operator| (Value v1, Value v2) => barithm_op(new(v1), new(v2), (_v1, _v2) => _v1. bor_eq(_v2));
    public static Value operator^ (Value v1, Value v2) => barithm_op(new(v1), new(v2), (_v1, _v2) => _v1. xor_eq(_v2));
    public static Value operator~ (Value v) => new((v.data is int) ? ~v.to_int() : throw new InvalidOperationException("Trying to use bitwise operations on non-integer types"));

    public static bool operator==(Value v1, Value v2) =>  v1.Equals(v2);
    public static bool operator!=(Value v1, Value v2) => !v1.Equals(v2);
    public static bool operator< (Value v1, Value v2) =>  v1.CompareTo(v2) <  0;
    public static bool operator<=(Value v1, Value v2) =>  v1.CompareTo(v2) <= 0;
    public static bool operator> (Value v1, Value v2) =>  v1.CompareTo(v2) >  0;
    public static bool operator>=(Value v1, Value v2) =>  v1.CompareTo(v2) >= 0;

    public object data{ get; private set; }

    public Value(bool data)          => this.data = data;
    public Value(char data)          => this.data = data;
    public Value(int data)           => this.data = data;
    public Value(float data)         => this.data = data;
    public Value(StringBuilder data) => this.data = data;

    public Value(Value other) => data = other.data switch{
        bool or char or int or float    => other.data,
        StringBuilder                sb => new StringBuilder(sb.ToString()),

        _ => throw new UnreachableException(),
    };

    public Value this[int i]{
        get => data switch{
            StringBuilder sb => new(sb[i]),

            _ => throw new ArgumentOutOfRangeException("Trying to use indexer on non-string"),
        };
        set{
            switch (data){
                case StringBuilder sb:
                    sb[i] = value.to_char();
                    break;
                default:
                    throw new ArgumentOutOfRangeException("Trying to use indexer on non-string");
            }
        }
    }

    public override bool Equals(object? obj) => Equals(obj as Value);
    public override int GetHashCode() => data.GetHashCode();
    public override string ToString() => data.ToString()!;

    public bool Equals(Value? other){
        if (other is null)
            return false;

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

        if (data is StringBuilder || other.data is StringBuilder){
            if (data is StringBuilder sb1 && other.data is StringBuilder sb2)
                return sb1.ToString().CompareTo(sb2.ToString());
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

        _ => throw new UnreachableException(),
    };
    public char to_char() => data switch{
        bool           b => (char)Convert.ToInt32(b),
        char           c => c,
        int            i => (char)i,
        float          f => (char)f,
        StringBuilder sb => char.Parse(sb.ToString()),

        _ => throw new UnreachableException(),
    };
    public int to_int() => data switch{
        bool           b => Convert.ToInt32(b),
        char           c => (int)c,
        int            i => i,
        float          f => (int)f,
        StringBuilder sb => int.Parse(sb.ToString()),

        _ => throw new UnreachableException(),
    };
    public float to_float() => data switch{
        bool           b => Convert.ToSingle(b),
        char           c => (float)c,
        int            i => (float)i,
        float          f => f,
        StringBuilder sb => float.Parse(sb.ToString()),

        _ => throw new UnreachableException(),
    };
    public StringBuilder to_string() => data switch{
        bool           b => new( b.ToString()),
        char           c => new( c.ToString()),
        int            i => new( i.ToString()),
        float          f => new( f.ToString()),
        StringBuilder sb => new(sb.ToString()),

        _ => throw new UnreachableException(),
    };

    public void add_eq(Value other){
        if (data is not StringBuilder && other.data is StringBuilder)
            throw new ArgumentOutOfRangeException("Trying to do addition on a non-string with a string");

        data = data switch{
            bool           b => b || other.to_bool(),
            char           c => (char)(c + other.to_char()),
            int            i => i + other.to_int(),
            float          f => f + other.to_float(),
            StringBuilder sb => other.data switch{
                char           other_c => sb.Append(other_c),
                StringBuilder other_sb => sb.Append(other_sb.ToString()),

                _ => throw new ArgumentOutOfRangeException("Trying to do addition on a string with bool, int or float"),
            },

            _ => throw new UnreachableException()
        };
    }
    public void sub_eq(Value other) => data = data switch{
        bool  b => Convert.ToBoolean(Convert.ToInt32(b) - other.to_int()),
        char  c => (char)(c - other.to_char()),
        int   i => i - other.to_int(),
        float f => f - other.to_float(),

        _ => throw new ArgumentOutOfRangeException("Trying to do subtraction with strings"),
    };
    public void mul_eq(Value other) => data = data switch{
        bool  b => b && other.to_bool(),
        char  c => (char)(c * other.to_char()),
        int   i => i * other.to_int(),
        float f => f * other.to_float(),

        _ => throw new ArgumentOutOfRangeException("Trying to do multiplication with strings"),
    };
    public void div_eq(Value other) => data = data switch{
        bool  b => Convert.ToBoolean(Convert.ToInt32(b) / other.to_int()),
        char  c => (char)(c / other.to_char()),
        int   i => i / other.to_int(),
        float f => f / other.to_float(),

        _ => throw new ArgumentOutOfRangeException("Trying to do division with strings"),
    };
    public void mod_eq(Value other) => data = data switch{
        bool  b => Convert.ToBoolean(Convert.ToInt32(b) % other.to_int()),
        char  c => (char)(c % other.to_char()),
        int   i => i % other.to_int(),
        float f => f % other.to_float(),

        _ => throw new ArgumentOutOfRangeException("Trying to do modulo with strings"),
    };
    public void pow_eq(Value other){
        if (data is StringBuilder || other.data is StringBuilder)
            throw new ArgumentOutOfRangeException("Trying to do exponentiation strings");

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
        if (data is not int || other.data is not int)
            throw new InvalidOperationException("Trying to use bitwise operations on non-integer types");

        data = to_int() << other.to_int();
    }
    public void shr_eq(Value other){
        if (data is not int || other.data is not int)
            throw new InvalidOperationException("Trying to use bitwise operations on non-integer types");

        data = to_int() >> other.to_int();
    }
    public void band_eq(Value other){
        if (data is not int || other.data is not int)
            throw new InvalidOperationException("Trying to use bitwise operations on non-integer types");

        data = to_int() & other.to_int();
    }
    public void bor_eq(Value other){
        if (data is not int || other.data is not int)
            throw new InvalidOperationException("Trying to use bitwise operations on non-integer types");

        data = to_int() | other.to_int();
    }
    public void xor_eq(Value other){
        if (data is not int || other.data is not int)
            throw new InvalidOperationException("Trying to use bitwise operations on non-integer types");

        data = to_int() ^ other.to_int();
    }
}
