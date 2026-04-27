using System.ComponentModel;
using System.Reflection;

namespace Alife.Function.Interpreter;

[AttributeUsage(AttributeTargets.Method)]
public class XmlFunctionAttribute(string? name = null, int order = 0) : Attribute
{
    public string? Name { get; } = name;
    public int Order { get; } = order;
}
[AttributeUsage(AttributeTargets.Parameter)]
public class XmlContentAttribute : Attribute { }
public class XmlContext
{
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }
    public string Content { get; set; } = "";
}
public class XmlHandler
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Explain { get; set; }
    public List<XmlFunction> Functions { get; init; } = new();
    public object? Instance { get; init; }
    public bool IsImplicit { get; set; }

    public XmlHandler() { }
    public XmlHandler(object instance, string? explain = null, bool isImplicit = false)
    {
        Instance = instance;
        Type handlerType = instance.GetType();
        Name = handlerType.Name;
        Description = handlerType.GetCustomAttribute<DescriptionAttribute>()?.Description;
        IsImplicit = isImplicit;
        Explain = explain;

        foreach (MethodInfo method in handlerType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            XmlFunction? function = ParseFunction(method, instance);
            if (function != null)
                Functions.Add(function);
        }
    }

    static XmlFunction? ParseFunction(MethodInfo method, object handler)
    {
        XmlFunctionAttribute? functionAttribute = method.GetCustomAttribute<XmlFunctionAttribute>();
        if (functionAttribute == null)
            return null;

        string name = functionAttribute.Name ?? method.Name.ToLower();
        string? description = method.GetCustomAttribute<DescriptionAttribute>()?.Description;

        ParameterInfo[] rawParameters = method.GetParameters();
        //统计参数信息
        int contextParameterIndex = -1;
        int contentParameterIndex = -1;
        Dictionary<string, int> normalParameterIndices = new();
        List<XmlParameter> normalParameters = new();
        string? contentName = null;
        string? contentDescription = null;
        for (int index = 0; index < rawParameters.Length; index++)
        {
            ParameterInfo parameterInfo = rawParameters[index];
            if (parameterInfo.Name == null)
                continue;

            string parameterName = parameterInfo.Name.ToLower();
            string? parameterDescription = parameterInfo.GetCustomAttribute<DescriptionAttribute>()?.Description;

            if (parameterInfo.ParameterType.IsAssignableTo(typeof(XmlContext)))
            {
                contextParameterIndex = index;
            }
            else if (parameterInfo.ParameterType == typeof(string).MakeByRefType() || parameterInfo.GetCustomAttribute<XmlContentAttribute>() != null)
            {
                contentParameterIndex = index;
                contentName = parameterName;
                contentDescription = parameterDescription;
            }
            else if (TypeDescriptor.GetConverter(parameterInfo.ParameterType).CanConvertFrom(typeof(string)))
            {
                bool isCanNull = Nullable.GetUnderlyingType(parameterInfo.ParameterType) != null;
                Type parameterType = isCanNull ? parameterInfo.ParameterType.GenericTypeArguments[0] : parameterInfo.ParameterType;
                string parameterTypeName = parameterType.IsEnum ? string.Join(" | ", parameterType.GetEnumNames()) : parameterType.Name;
                if (isCanNull)
                    parameterTypeName += "[可选]";

                normalParameters.Add(new XmlParameter() {
                    Name = parameterName,
                    Description = parameterDescription,
                    Type = parameterTypeName,
                });
                normalParameterIndices[parameterName] = index;
            }
            else
            {
                throw new NotSupportedException("不支持的参数类型！");
            }
        }

        //统计调用方法
        object?[] parameterValuesBuffer = new object?[rawParameters.Length];
        Task Invoker(XmlContext context)
        {
            //填充默认值
            for (int index = 0; index < rawParameters.Length; index++)
            {
                object? defaultValue = rawParameters[index].DefaultValue;
                if (defaultValue == null || defaultValue == DBNull.Value)
                {
                    Type parameterType = rawParameters[index].ParameterType;
                    defaultValue = parameterType.IsValueType ? Activator.CreateInstance(parameterType) : null;
                }
                parameterValuesBuffer[index] = defaultValue;
            }
            //接收输入值
            foreach ((string name, string value) in context.Parameters)
            {
                if (normalParameterIndices.TryGetValue(name, out int index) == false)
                    continue; //没有同名参数
                TypeConverter converter = TypeDescriptor.GetConverter(rawParameters[index].ParameterType);
                if (converter.CanConvertFrom(typeof(string)) == false)
                    continue; //无法通过字符串转换

                try
                {
                    object? result = converter.ConvertFromInvariantString(value);
                    parameterValuesBuffer[index] = result;
                }
                catch (Exception)
                {
                    // Console.WriteLine(e);
                }
            }
            //设置特殊值
            if (contextParameterIndex != -1 && rawParameters[contextParameterIndex].ParameterType.IsInstanceOfType(context))
                parameterValuesBuffer[contextParameterIndex] = context;
            if (contentParameterIndex != -1)
                parameterValuesBuffer[contentParameterIndex] = context.Content;

            //调用
            object? back = method.Invoke(handler, parameterValuesBuffer);

            //处理返回值
            if (contentParameterIndex != -1) context.Content = parameterValuesBuffer[contentParameterIndex] as string ?? "";
            if (back is Task task) return task;
            return Task.CompletedTask;
        }

        return new XmlFunction {
            Name = name,
            Description = description,
            ContentName = contentName,
            ContentDescription = contentDescription,
            Parameters = normalParameters,
            Invoker = Invoker,
            Order = functionAttribute.Order,
        };
    }
}
public class XmlFunction : IComparable<XmlFunction>
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? ContentName { get; init; }
    public string? ContentDescription { get; init; }
    public List<XmlParameter> Parameters { get; init; } = new();
    public required Func<XmlContext, Task> Invoker { get; init; }
    public int Order { get; init; }

    public int CompareTo(XmlFunction? other)
    {
        if (ReferenceEquals(this, other)) return 0;
        if (other is null) return 1;
        return Order.CompareTo(other.Order);
    }
}
public record XmlParameter
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Type { get; init; }
}
