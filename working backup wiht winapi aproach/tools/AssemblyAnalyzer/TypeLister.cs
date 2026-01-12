using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AssemblyAnalyzer
{
    class TypeLister
    {
        static void Main(string[] args)
        {
            string assemblyPath = args.Length > 0
                ? args[0]
                : @"C:\Users\fabia\arena\libs\Wizards.Arena.Models.dll";

            string outputPath = args.Length > 1
                ? args[1]
                : @"C:\Users\fabia\arena\typelist.txt";

            Console.WriteLine("Type Lister");
            Console.WriteLine("Loading: " + assemblyPath);

            try
            {
                var assembly = Assembly.ReflectionOnlyLoadFrom(assemblyPath);
                var types = new List<string>();

                foreach (var type in assembly.GetTypes())
                {
                    types.Add(type.FullName);
                }

                types.Sort();

                using (var writer = new StreamWriter(outputPath))
                {
                    writer.WriteLine("Types in: " + Path.GetFileName(assemblyPath));
                    writer.WriteLine("Total: " + types.Count);
                    writer.WriteLine(new string('=', 60));

                    foreach (var t in types)
                    {
                        writer.WriteLine(t);
                    }
                }

                Console.WriteLine("Found " + types.Count + " types");
                Console.WriteLine("Output: " + outputPath);
            }
            catch (ReflectionTypeLoadException ex)
            {
                Console.WriteLine("Partial load. Writing available types.");
                var types = ex.Types.Where(t => t != null).Select(t => t.FullName).OrderBy(n => n).ToList();

                using (var writer = new StreamWriter(outputPath))
                {
                    writer.WriteLine("Types in: " + Path.GetFileName(assemblyPath));
                    writer.WriteLine("Total: " + types.Count);
                    writer.WriteLine(new string('=', 60));

                    foreach (var t in types)
                    {
                        writer.WriteLine(t);
                    }
                }

                Console.WriteLine("Found " + types.Count + " types");
                Console.WriteLine("Output: " + outputPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
