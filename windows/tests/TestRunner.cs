using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Polishly.TestInfrastructure
{
    public static class TestRunner
    {
        public static async Task<int> Main(string[] args)
        {
            var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            Console.WriteLine($"=== Polishly XUnit Test Execution: {assembly.GetName().Name} ===");
            
            int totalExecuted = 0;
            int passed = 0;
            int failed = 0;

            var types = assembly.GetTypes();
            foreach (var type in types)
            {
                if (type.IsAbstract || !type.IsClass) continue;

                object? instance = null;
                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                foreach (var method in methods)
                {
                    var factAttr = method.GetCustomAttribute<FactAttribute>();
                    var theoryAttr = method.GetCustomAttribute<TheoryAttribute>();

                    if (factAttr == null && theoryAttr == null) continue;

                    if (instance == null)
                    {
                        try
                        {
                            var ctors = type.GetConstructors();
                            var outputCtor = ctors.FirstOrDefault(c => c.GetParameters().Length == 1 && typeof(Xunit.Abstractions.ITestOutputHelper).IsAssignableFrom(c.GetParameters()[0].ParameterType));
                            if (outputCtor != null)
                            {
                                instance = outputCtor.Invoke(new object[] { new Xunit.Abstractions.ConsoleTestOutputHelper() });
                            }
                            else
                            {
                                instance = Activator.CreateInstance(type);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[FAIL] Unable to instantiate test class {type.Name}: {ex.Message}");
                            failed++;
                            continue;
                        }
                    }

                    if (factAttr != null)
                    {
                        totalExecuted++;
                        try
                        {
                            var result = method.Invoke(instance, null);
                            if (result is Task task)
                            {
                                await task;
                            }
                            Console.WriteLine($"  [PASS] {type.Name}.{method.Name}");
                            passed++;
                        }
                        catch (Exception ex)
                        {
                            var inner = ex.InnerException ?? ex;
                            Console.WriteLine($"  [FAIL] {type.Name}.{method.Name}: {inner.Message}");
                            failed++;
                        }
                    }
                    else if (theoryAttr != null)
                    {
                        var inlineDatas = method.GetCustomAttributes<InlineDataAttribute>();
                        foreach (var data in inlineDatas)
                        {
                            totalExecuted++;
                            var paramsInfo = method.GetParameters();
                            var argsArray = ConvertArgs(data.Data, paramsInfo);
                            string paramStr = string.Join(", ", data.Data.Select(d => d?.ToString() ?? "null"));
                            try
                            {
                                var result = method.Invoke(instance, argsArray);
                                if (result is Task task)
                                {
                                    await task;
                                }
                                Console.WriteLine($"  [PASS] {type.Name}.{method.Name}({paramStr})");
                                passed++;
                            }
                            catch (Exception ex)
                            {
                                var inner = ex.InnerException ?? ex;
                                Console.WriteLine($"  [FAIL] {type.Name}.{method.Name}({paramStr}): {inner.Message}");
                                failed++;
                            }
                        }
                    }
                }
            }

            Console.WriteLine($"\nResult: {passed}/{totalExecuted} passed, {failed} failed.");
            return failed == 0 ? 0 : 1;
        }

        private static object?[] ConvertArgs(object?[] rawArgs, ParameterInfo[] paramsInfo)
        {
            if (rawArgs == null) return Array.Empty<object?>();
            if (rawArgs.Length != paramsInfo.Length) return rawArgs;
            var converted = new object?[rawArgs.Length];
            for (int i = 0; i < rawArgs.Length; i++)
            {
                var val = rawArgs[i];
                var targetType = paramsInfo[i].ParameterType;
                if (val == null)
                {
                    converted[i] = null;
                }
                else if (targetType.IsEnum && val is not Enum)
                {
                    converted[i] = Enum.ToObject(targetType, val);
                }
                else if (!targetType.IsAssignableFrom(val.GetType()))
                {
                    converted[i] = Convert.ChangeType(val, targetType);
                }
                else
                {
                    converted[i] = val;
                }
            }
            return converted;
        }
    }
}
