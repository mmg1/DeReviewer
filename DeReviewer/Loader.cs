using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using DeReviewer.KnowledgeBase;

namespace DeReviewer
{
    public static class Loader
    {
        public static IEnumerable<PatternGroup> GetPatternGroups()
        {
            foreach (var type in GetCaseTypes())
            {
                var context = Context.CreateToAnalyze();

                // TODO: log returned errors
                ExecuteCase(context, type, GetCaseMethods(type));
                yield return new PatternGroup(type.Name, context.Patterns);
            }
        }

        public static PatternGroup GetPatternGroup(Type type, string methodName)
        {
            var context = Context.CreateToAnalyze();
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);

            // TODO: log returned errors
            ExecuteCase(context, type, new[] {method});
            return new PatternGroup(type.Name, context.Patterns);
        }

        public static string GetPayload()
        {
            throw new NotImplementedException();
        }

        public static List<string> ExecuteCase(Context context, Type type, IEnumerable<MethodInfo> methods)
        {
            var errors = new List<string>();

            object instance;
            try
            {
                instance = CreateObjectOf(type);
            }
            catch (Exception e)
            {
                errors.Add($"{Title(type)} Error default constructor calling. {e}");
                return errors;
            }

            try
            {
                var method = type.GetMethod(nameof(Case.Initialize),
                    BindingFlags.Public | BindingFlags.Instance);
                var initialize = BuildInitializeMethod(instance, method);
                initialize(context);
            }
            catch (Exception e)
            {
                errors.Add($"{Title(type)} Error initialize. {e}");
                return errors;
            }


            foreach (var method in methods)
            {
                try
                {
                    var testCase = BuildAction(instance, method);
                    testCase();
                }
                catch (Exception e)
                {
                    errors.Add($"{Title(type, method)} {e}");
                }
            }

            return errors;
        }

        public static IEnumerable<Type> GetCaseTypes() =>
            typeof(Case).Assembly.GetTypes()
                .Where(t => typeof(Case).IsAssignableFrom(t) && t != typeof(Case));

        public static IEnumerable<MethodInfo> GetCaseMethods(Type type) =>
            type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType == type); //skip Object methods and Case.Initialize()

        private static object CreateObjectOf(Type type)
        {
            var defaultCtor = Expression.Lambda<Func<object>>(Expression.New(type)).Compile();
            return defaultCtor();
        }

        private static Action BuildAction(object instance, MethodInfo method)
        {
            if (method == null)
            {
                throw new Exception("The public(!) instance(!) method has not found");
            }

            var instanceExpression = Expression.Constant(instance);
            return Expression.Lambda<Action>(
                    Expression.Call(instanceExpression, method))
                .Compile();
        }

        private static Action<Context> BuildInitializeMethod(object instance, MethodInfo method)
        {
            if (method == null)
            {
                throw new Exception("The Case.Initialize() method has not found");
            }

            var parameterExpression = Expression.Parameter(typeof(Context));
            var instanceExpression = Expression.Constant(instance);
            return Expression.Lambda<Action<Context>>(
                    Expression.Call(instanceExpression, method, parameterExpression), parameterExpression)
                .Compile();
        }

        private static string Title(Type type, MethodInfo method = null) =>
            method == null ? $"{type.Name}:" : $"{type.Name}.{method.Name}():";
    }
}