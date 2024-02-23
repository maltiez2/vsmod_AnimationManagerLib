using System;
using System.Reflection;

namespace AnimationManagerLib.Patches;

internal sealed class Field<TValue, TInstance>
{
    public TValue? Value
    {
        get
        {
            return (TValue?)mFieldInfo?.GetValue(mInstance);
        }
        set
        {
            mFieldInfo?.SetValue(mInstance, value);
        }
    }

    private readonly FieldInfo? mFieldInfo;
    private readonly TInstance mInstance;

    public Field(Type from, string field, TInstance instance)
    {
        mInstance = instance;
        mFieldInfo = from.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
    }
}

internal sealed class Property<TValue, TInstance>
{
    public TValue? Value
    {
        get
        {
            return (TValue?)mPropertyInfo?.GetValue(mInstance);
        }
        set
        {
            mPropertyInfo?.SetValue(mInstance, value);
        }
    }

    private readonly PropertyInfo? mPropertyInfo;
    private readonly TInstance mInstance;

    public Property(Type from, string property, TInstance instance)
    {
        mInstance = instance;
        mPropertyInfo = from.GetProperty(property, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
    }
}
