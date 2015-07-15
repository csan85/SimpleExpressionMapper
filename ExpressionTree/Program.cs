using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq.Expressions;

namespace ExpressionTree
{
    public class SimpleClass
    {
        public string Prova1 { get; set; }
        public int Prova2 { get; set; }
        public System.DateTime Prova3 { get; set; }
        public System.Guid Prova4 { get; set; }
        public decimal Prova5 { get; set; }
    }

    public class Copia
    {
        public string Prova1 { get; set; }
        public int Prova2 { get; set; }
        public System.DateTime Prova3 { get; set; }
        public System.Guid Prova4 { get; set; }
        public string Prova5 { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            MapperUsage();
            MapperListUsage();

            Performance();

            Console.ReadLine();
        }

        private static void MapperUsage()
        {
            ExpressionMapper.CreateMap<SimpleClass, Copia>();

            SimpleClass simpleClass = new SimpleClass()
            {
                Prova1 = "qualcosa",
                Prova2 = 124,
                Prova3 = System.DateTime.Today,
                Prova4 = System.Guid.NewGuid(),
                Prova5 = 123.567m
            };

            Copia copia = ExpressionMapper.Map<SimpleClass, Copia>(simpleClass);
        }

        private static void MapperListUsage()
        {
            ExpressionMapper.CreateMap<List<SimpleClass>, List<Copia>>();

            SimpleClass simpleClass = new SimpleClass()
            {
                Prova1 = "qualcosa",
                Prova2 = 124,
                Prova3 = System.DateTime.Today,
                Prova4 = System.Guid.NewGuid(),
                Prova5 = 123.567m
            };

            List<Copia> copia = ExpressionMapper.Map<List<SimpleClass>, List<Copia>>(new List<SimpleClass>() { simpleClass, new SimpleClass() });
        }

        private static void Performance()
        {
            SimpleClass simpleClass = new SimpleClass()
            {
                Prova1 = "qualcosa",
                Prova2 = 124,
                Prova3 = System.DateTime.Today,
                Prova4 = System.Guid.NewGuid(),
                Prova5 = 123.567m
            };

            Stopwatch watch = new Stopwatch();
            Console.WriteLine("---------- Get Properties ----------");

            watch.Restart();
            Type type = typeof(SimpleClass);
            PropertyInfo[] propertyInfos = type.GetProperties();
            for (int i = 0; i < 100000; ++i)
            {
                foreach (PropertyInfo info in propertyInfos)
                {
                    info.GetValue(simpleClass, null);
                }
            }
            watch.Stop();
            Console.WriteLine(String.Format("{0} Reflection", watch.Elapsed));

            watch.Restart();
            type = typeof(SimpleClass);
            ParameterExpression parameter = Expression.Parameter(type, "c");
            List<BinaryExpression> assignments = new List<BinaryExpression>();
            List<ParameterExpression> variables = new List<ParameterExpression>();
            foreach (PropertyInfo info in type.GetProperties())
            {
                ParameterExpression variable = Expression.Variable(info.PropertyType);
                BinaryExpression assignment = Expression.Assign(variable, Expression.Property(parameter, info));

                variables.Add(variable);
                assignments.Add(assignment);
            }

            Action<SimpleClass> action = Expression.Lambda<Action<SimpleClass>>(Expression.Block(variables, assignments), parameter).Compile();

            for (int i = 0; i < 100000; ++i)
            {
                action(simpleClass);
            }
            watch.Stop();
            Console.WriteLine(String.Format("{0} Expression", watch.Elapsed));

            watch.Restart();
            for (int i = 0; i < 100000; ++i)
            {
                action(simpleClass);
            }
            watch.Stop();
            Console.WriteLine(String.Format("{0} Created Expression", watch.Elapsed));

            watch.Restart();
            Action<SimpleClass> simpleAction = (c) =>
            {
                string obj1 = c.Prova1;
                int obj2 = c.Prova2;
                DateTime obj3 = c.Prova3;
                Guid obj4 = c.Prova4;
                decimal obj5 = c.Prova5;
            };
            for (int i = 0; i < 100000; ++i)
            {
                simpleAction(simpleClass);
            }
            watch.Stop();
            Console.WriteLine(String.Format("{0} Property", watch.Elapsed));

            Console.WriteLine();
            Console.WriteLine("---------- Constructor ----------");

            watch.Restart();
            for (int i = 0; i < 100000; ++i)
            {
                typeof(SimpleClass).GetConstructor(new Type[] { }).Invoke(new object[] { });
            }
            watch.Stop();
            Console.WriteLine(String.Format("{0} Reflection", watch.Elapsed));

            watch.Restart();
            for (int i = 0; i < 100000; ++i)
            {
                Activator.CreateInstance<SimpleClass>();
            }
            watch.Stop();
            Console.WriteLine(String.Format("{0} Activator", watch.Elapsed));

            watch.Restart();
            for (int i = 0; i < 100000; ++i)
            {
                Func<SimpleClass> function1 = Expression.Lambda<Func<SimpleClass>>(Expression.New(typeof(SimpleClass))).Compile();
                function1();
            }
            watch.Stop();
            Console.WriteLine(String.Format("{0} Expression sempre generata", watch.Elapsed));

            watch.Restart();
            Func<SimpleClass> function = Expression.Lambda<Func<SimpleClass>>(Expression.New(typeof(SimpleClass))).Compile();
            for (int i = 0; i < 100000; ++i)
            {
                function();
            }
            watch.Stop();
            Console.WriteLine(String.Format("{0} Expression", watch.Elapsed));

            watch.Restart();
            for (int i = 0; i < 100000; ++i)
            {
                CreateNew<SimpleClass>();
            }
            watch.Stop();
            Console.WriteLine(String.Format("{0} T con new()", watch.Elapsed));

            watch.Restart();
            for (int i = 0; i < 100000; ++i)
            {
                new SimpleClass();
            }
            watch.Stop();
            Console.WriteLine(String.Format("{0} new SimpleClass()", watch.Elapsed));
        }

        private static T CreateNew<T>() where T : new()
        {
            return new T();
        }
    }
}
