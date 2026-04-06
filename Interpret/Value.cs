namespace Interpret;

using System.Diagnostics;

public sealed class Value{
    static Value arithm_op(Value v1, Value v2, Action<Value, Value> op){
        Value temp = v1;

        switch (v1.m_data){
            case bool:
                switch (v2.m_data){
                    case bool:
                        break;
                    case int:
                        temp.m_data = v1.to_int();
                        break;
                    case float:
                        temp.m_data = v1.to_float();
                        break;
                    default:
                        throw new UnreachableException();
                }
                break;
            case int:
                switch (v2.m_data){
                    case bool:
                    case int:
                        break;
                    case float:
                        temp.m_data = v1.to_float();
                        break;
                    default:
                        throw new UnreachableException();
                }
                break;
            case float:
                break;
            default:
                throw new UnreachableException();
        }

        op(temp, v2);

        return temp;
    }

    static bool cmp_op(Value v1, Value v2, Func<int, int, bool> int_cmp, Func<float, float, bool> float_cmp){
        switch (v1.m_data){
            case bool:
            case int:
                switch (v2.m_data){
                    case bool:
                    case int:
                        return int_cmp(v1.to_int(), v2.to_int());
                    case float:
                        return float_cmp(v1.to_float(), v2.to_float());
                    default:
                        throw new UnreachableException();
                }
            case float:
                switch (v2.m_data){
                    case bool:
                    case int:
                    case float:
                        return float_cmp(v1.to_float(), v2.to_float());
                    default:
                        throw new UnreachableException();
                }
            default:
                throw new UnreachableException();
        }
    }

    public static Value operator+(Value v) => new(v);
    public static Value operator-(Value v) => v.m_data switch{
        bool  => new(!(bool)v.m_data),
        int   => new(-(int)v.m_data),
        float => new(-(float)v.m_data),

        _ => throw new UnreachableException(),
    };

    public static Value operator+(Value v1, Value v2) => arithm_op(new(v1), new(v2), (v1_, v2_) => v1_ += v2_);
    public static Value operator-(Value v1, Value v2) => arithm_op(new(v1), new(v2), (v1_, v2_) => v1_ -= v2_);
    public static Value operator*(Value v1, Value v2) => arithm_op(new(v1), new(v2), (v1_, v2_) => v1_ *= v2_);
    public static Value operator/(Value v1, Value v2) => arithm_op(new(v1), new(v2), (v1_, v2_) => v1_ /= v2_);
    public static Value operator%(Value v1, Value v2) => arithm_op(new(v1), new(v2), (v1_, v2_) => v1_ %= v2_);

    public static bool operator< (Value v1, Value v2) => cmp_op(v1, v2, (i1, i2) => (i1 <  i2), (f1, f2) => (f1 <  f2));
    public static bool operator<=(Value v1, Value v2) => cmp_op(v1, v2, (i1, i2) => (i1 <= i2), (f1, f2) => (f1 <= f2));
    public static bool operator> (Value v1, Value v2) => cmp_op(v1, v2, (i1, i2) => (i1 >  i2), (f1, f2) => (f1 >  f2));
    public static bool operator>=(Value v1, Value v2) => cmp_op(v1, v2, (i1, i2) => (i1 >= i2), (f1, f2) => (f1 >= f2));
    public static bool operator==(Value v1, Value v2) => cmp_op(v1, v2, (i1, i2) => (i1 == i2), (f1, f2) => (f1 == f2));
    public static bool operator!=(Value v1, Value v2) => cmp_op(v1, v2, (i1, i2) => (i1 != i2), (f1, f2) => (f1 != f2));

    object m_data;

    public Value(bool data)  => m_data = data;
    public Value(int data)   => m_data = data;
    public Value(float data) => m_data = data;

    public Value(Value other) => m_data = other.m_data;

    public override string ToString() => m_data.ToString()!;

    public override bool Equals(object? obj) => obj is Value && this == (Value)obj;

    public override int GetHashCode() => m_data.GetHashCode();

    public bool to_bool() => m_data switch{
        bool  => (bool)m_data,
        int   => Convert.ToBoolean(m_data),
        float => Convert.ToBoolean(m_data),

        _ => throw new UnreachableException()
    };
    public int to_int() => m_data switch{
        bool  => Convert.ToInt32(m_data),
        int   => (int)m_data,
        float => (int)((float)m_data),

        _ => throw new UnreachableException()
    };
    public float to_float() => m_data switch{
        bool  => Convert.ToSingle(m_data),
        int   => (float)((int)m_data),
        float => (float)m_data,

        _ => throw new UnreachableException()
    };

    public void operator+=(Value v) => m_data = m_data switch{
        bool  => (bool)m_data || v.to_bool(),
        int   => (int)m_data + v.to_int(),
        float => (float)m_data + v.to_float(),

        _ => throw new UnreachableException()
    };
    public void operator-=(Value v) => m_data = m_data switch{
        bool  => Convert.ToBoolean(to_int() - v.to_int()),
        int   => (int)m_data - v.to_int(),
        float => (float)m_data - v.to_float(),

        _ => throw new UnreachableException()
    };
    public void operator*=(Value v) => m_data = m_data switch{
        bool  => (bool)m_data && v.to_bool(),
        int   => (int)m_data * v.to_int(),
        float => (float)m_data * v.to_float(),

        _ => throw new UnreachableException()
    };
    public void operator/=(Value v) => m_data = m_data switch{
        bool  => Convert.ToBoolean(to_int() / v.to_int()),
        int   => (int)m_data / v.to_int(),
        float => (float)m_data / v.to_float(),

        _ => throw new UnreachableException()
    };
    public void operator%=(Value v) => m_data = m_data switch{
        bool  => Convert.ToBoolean(to_int() % v.to_int()),
        int   => (int)m_data % v.to_int(),
        float => (float)m_data % v.to_float(),

        _ => throw new UnreachableException()
    };
}
