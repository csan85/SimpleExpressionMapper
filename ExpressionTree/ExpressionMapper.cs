using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace ExpressionTree
{
    public static class ExpressionMapper
    {
        private static Dictionary<Tuple<Type, Type>, Lazy<Delegate>> mapDelegate = new Dictionary<Tuple<Type, Type>, Lazy<Delegate>>();

        public static void CreateMap<TOrigin, TDestination>()
            where TOrigin : class
            where TDestination : class
        {
            CreateDelegate(typeof(TOrigin), typeof(TDestination));
        }

        public static TDestination Map<TOrigin, TDestination>(TOrigin origin)
            where TOrigin : class
            where TDestination : class
        {
            Lazy<Delegate> @delegate = null;
            if (mapDelegate.TryGetValue(new Tuple<Type, Type>(typeof(TOrigin), typeof(TDestination)), out @delegate))
            {
                return ((Func<TOrigin, TDestination>)@delegate.Value)(origin);
            }

            return null;
        }


        private static void CreateDelegate(Type origin, Type destination)
        {
            Type originTypeToMap = null;
            Type destinationTypeToMap = null;

            bool isOriginEnumerable = false;
            bool isDestinationEnumerable = false;
            if (origin.IsGenericType && destination.IsGenericType)
            {
                Type originArgumentType = origin.GenericTypeArguments[0];
                Type destinationArgumentType = destination.GenericTypeArguments[0];

                isOriginEnumerable = origin.IsAssignableFrom(typeof(List<>).MakeGenericType(originArgumentType));
                isDestinationEnumerable = destination.IsAssignableFrom(typeof(List<>).MakeGenericType(destinationArgumentType));

                if (isOriginEnumerable && isDestinationEnumerable)
                {
                    originTypeToMap = origin.GenericTypeArguments[0];
                    destinationTypeToMap = destination.GenericTypeArguments[0];
                }
            }
            else
            {
                originTypeToMap = origin;
                destinationTypeToMap = destination;
            }

            var parameterOrigin = Expression.Parameter(origin, "o");

            Lazy<Delegate> @delegate = null;
            if (isOriginEnumerable && isDestinationEnumerable)
            {
                var expression = CreateForeachExpression(origin, destination, originTypeToMap, destinationTypeToMap, parameterOrigin);
                Type delegateType = typeof(Func<,>).MakeGenericType(origin, destination);

                Func<Delegate> createFunction = new Func<Delegate>(() =>
                    Expression.Lambda(delegateType, expression, parameterOrigin).Compile());

                @delegate = new Lazy<Delegate>(createFunction);
            }
            else
            {
                var expression = CreateMapByBinding(origin, destination, parameterOrigin);
                Type delegateType = typeof(Func<,>).MakeGenericType(origin, destination);

                Func<Delegate> createFunction = new Func<Delegate>(() =>
                    Expression.Lambda(delegateType, expression, parameterOrigin).Compile());

                @delegate = new Lazy<Delegate>(createFunction);
            }

            mapDelegate.Add(new Tuple<Type, Type>(origin, destination), @delegate);
        }
        
        public static Expression CreateMapByBinding(Type origin, Type destination, ParameterExpression parameterOrigin)
        {
            Dictionary<string, PropertyInfo> dictionaryInfoOrigine = origin
                .GetProperties()
                .Where(p => p.CanRead)
                .ToDictionary(i => i.Name, i => i);

            IEnumerable<PropertyInfo> infoDestinazione = destination
                .GetProperties()
                .Where(p => p.CanWrite);

            List<MemberBinding> bindings = new List<MemberBinding>();
            foreach (PropertyInfo info in infoDestinazione)
            {
                PropertyInfo sourceInfo = null;
                if (dictionaryInfoOrigine.TryGetValue(info.Name, out sourceInfo) && info.PropertyType == sourceInfo.PropertyType)
                {
                    var binding = Expression.Bind(info, Expression.Property(parameterOrigin, sourceInfo));
                    bindings.Add(binding);
                }
                else if (sourceInfo != null && info.PropertyType == typeof(string))
                {
                    var property = Expression.Property(parameterOrigin, sourceInfo);
                    var propertyToString = Expression.Call(property, sourceInfo.PropertyType.GetMethod("ToString", new Type[] { }));

                    var binding = Expression.Bind(info, propertyToString);
                    bindings.Add(binding);
                }
            }

            return Expression.MemberInit(Expression.New(destination), bindings);
        }

        private static Expression CreateForeachExpression(Type origin, Type destination, Type originTypeToMap, Type destinationTypeToMap,
                                                          ParameterExpression parameterOrigin)
        {
            var index = Expression.Variable(typeof(int), "i");
            var item = Expression.Variable(originTypeToMap, "item");
            var itemToAdd = Expression.Variable(destinationTypeToMap, "itemToAdd");
            var resultList = Expression.Variable(destination, "restultCollection");
            var list = Expression.Variable(origin, "origin");

            var @break = Expression.Label();

            /*
             * https://msdn.microsoft.com/en-us/library/dd324074(v=vs.100).aspx
             * si trova scritto che:
             *  - Remarks: When the block expression is executed, it returns the value of the last expression in the block.
             */

            var body = CreateMapByBinding(originTypeToMap, destinationTypeToMap, item);

            var expression = Expression.Block(
                new ParameterExpression[] { item, index, resultList, itemToAdd, list },
                Expression.Assign(list, parameterOrigin),
                Expression.Assign(resultList, Expression.New(destination)),
                Expression.Assign(index, Expression.Constant(0)),
                Expression.Loop(
                    Expression.IfThenElse(
                        Expression.LessThan(index, Expression.Property(list, origin.GetProperty("Count"))),
                        Expression.Block(
                            Expression.Assign(item, Expression.MakeIndex(list, origin.GetProperty("Item"), new Expression[] { index })),
                            Expression.Assign(itemToAdd, body),
                            Expression.Call(resultList, destination.GetMethod("Add"), itemToAdd),
                            Expression.AddAssign(index, Expression.Constant(1))
                        ),
                        Expression.Break(@break))
                , @break),
                resultList
            );
            return expression;
        }
    }
}
