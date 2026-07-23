using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Xunit
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class FactAttribute : Attribute
    {
        public string? Skip { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class TheoryAttribute : Attribute
    {
        public string? Skip { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class InlineDataAttribute : Attribute
    {
        public object?[] Data { get; }
        public InlineDataAttribute(params object?[] data)
        {
            Data = data;
        }
    }

    public static class Assert
    {
        public static void True(bool condition, string? userMessage = null)
        {
            if (!condition)
                throw new Exception(userMessage ?? "Assert.True failure: expected true, got false.");
        }

        public static void False(bool condition, string? userMessage = null)
        {
            if (condition)
                throw new Exception(userMessage ?? "Assert.False failure: expected false, got true.");
        }

        public static void Null(object? @object)
        {
            if (@object != null)
                throw new Exception($"Assert.Null failure: expected null, got {@object}.");
        }

        public static void NotNull(object? @object)
        {
            if (@object == null)
                throw new Exception("Assert.NotNull failure: expected non-null value.");
        }

        public static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception($"Assert.Equal failure. Expected: '{expected}', Actual: '{actual}'");
        }

        public static void NotEqual<T>(T expected, T actual)
        {
            if (EqualityComparer<T>.Default.Equals(expected, actual))
                throw new Exception($"Assert.NotEqual failure. Both values are '{expected}'.");
        }

        public static void Contains(string expectedSubstring, string? actualString)
        {
            if (actualString == null || !actualString.Contains(expectedSubstring))
                throw new Exception($"Assert.Contains failure. Substring '{expectedSubstring}' not found in '{actualString}'.");
        }

        public static void Contains(string expectedSubstring, string? actualString, StringComparison comparisonType)
        {
            if (actualString == null || !actualString.Contains(expectedSubstring, comparisonType))
                throw new Exception($"Assert.Contains failure. Substring '{expectedSubstring}' not found in '{actualString}'.");
        }

        public static void Contains<T>(IEnumerable<T> collection, Func<T, bool> filter)
        {
            if (collection == null || !collection.Any(filter))
                throw new Exception("Assert.Contains failure. Matching element not found in collection.");
        }

        public static void Contains<T>(T expected, IEnumerable<T>? collection)
        {
            if (collection == null || !collection.Contains(expected))
                throw new Exception($"Assert.Contains failure. Item '{expected}' not found in collection.");
        }

        public static void DoesNotContain(string expectedSubstring, string? actualString)
        {
            if (actualString != null && actualString.Contains(expectedSubstring))
                throw new Exception($"Assert.DoesNotContain failure. Substring '{expectedSubstring}' was found in '{actualString}'.");
        }

        public static void DoesNotContain<T>(T expected, IEnumerable<T>? collection)
        {
            if (collection != null && collection.Contains(expected))
                throw new Exception($"Assert.DoesNotContain failure. Item '{expected}' was found in collection.");
        }

        public static void NotEmpty(string? value)
        {
            if (string.IsNullOrEmpty(value))
                throw new Exception("Assert.NotEmpty failure. String was null or empty.");
        }

        public static void NotEmpty<T>(IEnumerable<T>? collection)
        {
            if (collection == null || !collection.Any())
                throw new Exception("Assert.NotEmpty failure. Collection was null or empty.");
        }

        public static void Empty<T>(IEnumerable<T>? collection)
        {
            if (collection != null && collection.Any())
                throw new Exception("Assert.Empty failure. Collection contained elements.");
        }

        public static void Single<T>(IEnumerable<T>? collection)
        {
            if (collection == null || collection.Count() != 1)
                throw new Exception($"Assert.Single failure. Expected 1 element, got {(collection == null ? 0 : collection.Count())}.");
        }

        public static async Task<T> ThrowsAsync<T>(Func<Task> testCode) where T : Exception
        {
            try
            {
                await testCode();
            }
            catch (T ex)
            {
                return ex;
            }
            catch (Exception ex)
            {
                throw new Exception($"Assert.ThrowsAsync failure. Expected exception of type '{typeof(T).Name}', but '{ex.GetType().Name}' was thrown: {ex.Message}");
            }
            throw new Exception($"Assert.ThrowsAsync failure. Expected exception of type '{typeof(T).Name}', but no exception was thrown.");
        }

        public static void Same(object? expected, object? actual)
        {
            if (!object.ReferenceEquals(expected, actual))
                throw new Exception("Assert.Same failure. Objects are not the same instance.");
        }

        public static void NotSame(object? expected, object? actual)
        {
            if (object.ReferenceEquals(expected, actual))
                throw new Exception("Assert.NotSame failure. Objects are the same instance.");
        }

        public static void InRange<T>(T actual, T low, T high) where T : IComparable<T>
        {
            if (actual.CompareTo(low) < 0 || actual.CompareTo(high) > 0)
                throw new Exception($"Assert.InRange failure. Value '{actual}' is outside range [{low}, {high}].");
        }

        public static void StartsWith(string expectedPrefix, string? actualString)
        {
            if (actualString == null || !actualString.StartsWith(expectedPrefix))
                throw new Exception($"Assert.StartsWith failure. Value '{actualString}' does not start with '{expectedPrefix}'.");
        }

        public static void EndsWith(string expectedSuffix, string? actualString)
        {
            if (actualString == null || !actualString.EndsWith(expectedSuffix))
                throw new Exception($"Assert.EndsWith failure. Value '{actualString}' does not end with '{expectedSuffix}'.");
        }
    }

    public static class Record
    {
        public static Exception? Exception(Action testCode)
        {
            try
            {
                testCode();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        public static async Task<Exception?> ExceptionAsync(Func<Task> testCode)
        {
            try
            {
                await testCode();
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }
}

namespace Xunit.Abstractions
{
    public interface ITestOutputHelper
    {
        void WriteLine(string message);
        void WriteLine(string format, params object[] args);
    }

    public class ConsoleTestOutputHelper : ITestOutputHelper
    {
        public void WriteLine(string message) => Console.WriteLine(message);
        public void WriteLine(string format, params object[] args) => Console.WriteLine(format, args);
    }
}
