using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AssemblyAnalyzer
{
    class Program
    {
        static readonly string[] ClassKeywords = new[]
        {
            "Card", "Deck", "Hand", "Battlefield", "Graveyard", "Library", "Zone",
            "UI", "Screen", "Panel", "View", "Controller", "Manager",
            "Game", "Turn", "Phase", "Match", "Player",
            "Tooltip", "Hover", "Select", "Focus", "Input",
            "Text", "Label", "Display", "Info", "Detail",
            "Login", "Auth", "Account", "Register", "Password", "Email",
            "Age", "Verification", "Welcome", "Splash", "Loading",
            "Menu", "Navigation", "Scene", "Dialog", "Modal", "Popup",
            "Button", "Field", "Form", "Submit"
        };

        static readonly string[] MethodKeywords = new[]
        {
            "GetName", "GetText", "GetTitle", "GetDescription", "GetOracle",
            "GetMana", "GetCost", "GetPower", "GetToughness", "GetType",
            "Display", "Show", "Render", "Update", "Set",
            "OnHover", "OnSelect", "OnClick", "OnFocus"
        };

        static void Main(string[] args)
        {
            string assemblyPath = args.Length > 0
                ? args[0]
                : @"C:\Users\fabia\arena\libs\Assembly-CSharp.dll";

            string outputPath = args.Length > 1
                ? args[1]
                : @"C:\Users\fabia\arena\analysis_results.txt";

            Console.WriteLine("MTGA Assembly Analyzer");
            Console.WriteLine("Loading: " + assemblyPath);

            try
            {
                var results = AnalyzeAssembly(assemblyPath);
                WriteResults(results, outputPath);
                Console.WriteLine("Analysis complete. Results written to: " + outputPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }

        static List<ClassInfo> AnalyzeAssembly(string path)
        {
            var results = new List<ClassInfo>();
            var assembly = Assembly.LoadFrom(path);

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                Console.WriteLine("Warning: Some types could not be loaded. Processing available types.");
                Console.WriteLine("Loaded " + ex.Types.Count(t => t != null) + " types out of attempted load.");
                types = ex.Types.Where(t => t != null).ToArray();
            }

            Console.WriteLine("Total types in assembly: " + types.Length);

            // Print first 20 type names for debugging
            Console.WriteLine("Sample type names:");
            foreach (var t in types.Take(20))
            {
                Console.WriteLine("  " + t.FullName);
            }

            foreach (var type in types)
            {
                try
                {
                    if (!IsRelevantClass(type.Name))
                        continue;

                    var classInfo = new ClassInfo();
                    classInfo.Name = type.FullName;
                    classInfo.BaseClass = type.BaseType != null ? type.BaseType.Name : "None";
                    classInfo.IsPublic = type.IsPublic;
                    classInfo.Properties = new List<string>();
                    classInfo.Methods = new List<string>();
                    classInfo.Events = new List<string>();

                    try
                    {
                        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                        {
                            try
                            {
                                classInfo.Properties.Add(string.Format("{0} {1}", prop.PropertyType.Name, prop.Name));
                            }
                            catch { }
                        }
                    }
                    catch { }

                    try
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                        {
                            try
                            {
                                if (method.IsSpecialName) continue;

                                var paramList = new List<string>();
                                foreach (var p in method.GetParameters())
                                {
                                    paramList.Add(string.Format("{0} {1}", p.ParameterType.Name, p.Name));
                                }
                                var parameters = string.Join(", ", paramList);
                                classInfo.Methods.Add(string.Format("{0} {1}({2})", method.ReturnType.Name, method.Name, parameters));
                            }
                            catch { }
                        }
                    }
                    catch { }

                    try
                    {
                        foreach (var evt in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                        {
                            try
                            {
                                var handlerName = evt.EventHandlerType != null ? evt.EventHandlerType.Name : "Unknown";
                                classInfo.Events.Add(string.Format("{0} {1}", handlerName, evt.Name));
                            }
                            catch { }
                        }
                    }
                    catch { }

                    results.Add(classInfo);
                }
                catch { }
            }

            return results.OrderBy(c => c.Name).ToList();
        }

        static bool IsRelevantClass(string name)
        {
            return ClassKeywords.Any(keyword =>
                name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static void WriteResults(List<ClassInfo> results, string outputPath)
        {
            using (var writer = new StreamWriter(outputPath))
            {
                writer.WriteLine("MTGA Assembly Analysis Results");
                writer.WriteLine("Generated: " + DateTime.Now);
                writer.WriteLine("Total classes found: " + results.Count);
                writer.WriteLine(new string('=', 60));
                writer.WriteLine();

                var loginClasses = results.Where(c => ContainsKeyword(c.Name, "Login", "Auth", "Account", "Register", "Password", "Email", "Age", "Verification")).ToList();
                var menuClasses = results.Where(c => ContainsKeyword(c.Name, "Menu", "Navigation", "Welcome", "Splash", "Loading")).ToList();
                var dialogClasses = results.Where(c => ContainsKeyword(c.Name, "Dialog", "Modal", "Popup", "Form")).ToList();
                var cardClasses = results.Where(c => ContainsKeyword(c.Name, "Card", "Deck", "Hand", "Zone", "Battlefield", "Graveyard", "Library")).ToList();
                var uiClasses = results.Where(c => ContainsKeyword(c.Name, "UI", "Screen", "Panel", "View", "Display", "Button", "Field")).ToList();
                var gameClasses = results.Where(c => ContainsKeyword(c.Name, "Game", "Turn", "Phase", "Match", "Player")).ToList();
                var inputClasses = results.Where(c => ContainsKeyword(c.Name, "Tooltip", "Hover", "Select", "Focus", "Input")).ToList();
                var sceneClasses = results.Where(c => ContainsKeyword(c.Name, "Scene", "Controller", "Manager")).ToList();

                WriteSection(writer, "LOGIN AND AUTHENTICATION CLASSES", loginClasses);
                WriteSection(writer, "MENU AND NAVIGATION CLASSES", menuClasses);
                WriteSection(writer, "DIALOG AND POPUP CLASSES", dialogClasses);
                WriteSection(writer, "SCENE AND CONTROLLER CLASSES", sceneClasses);
                WriteSection(writer, "CARD RELATED CLASSES", cardClasses);
                WriteSection(writer, "UI RELATED CLASSES", uiClasses);
                WriteSection(writer, "GAME FLOW CLASSES", gameClasses);
                WriteSection(writer, "INPUT AND INTERACTION CLASSES", inputClasses);

                writer.WriteLine(new string('=', 60));
                writer.WriteLine("INTERESTING METHODS (text/display related)");
                writer.WriteLine(new string('=', 60));
                writer.WriteLine();

                foreach (var cls in results)
                {
                    var interestingMethods = cls.Methods.Where(m =>
                        MethodKeywords.Any(k => m.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();

                    if (interestingMethods.Any())
                    {
                        writer.WriteLine("Class: " + cls.Name);
                        foreach (var method in interestingMethods)
                        {
                            writer.WriteLine("  Method: " + method);
                        }
                        writer.WriteLine();
                    }
                }
            }
        }

        static bool ContainsKeyword(string name, params string[] keywords)
        {
            return keywords.Any(k => name.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        static void WriteSection(StreamWriter writer, string title, List<ClassInfo> classes)
        {
            writer.WriteLine(new string('=', 60));
            writer.WriteLine(title);
            writer.WriteLine("Count: " + classes.Count);
            writer.WriteLine(new string('=', 60));
            writer.WriteLine();

            foreach (var cls in classes)
            {
                writer.WriteLine("CLASS: " + cls.Name);
                writer.WriteLine("Base: " + cls.BaseClass);
                writer.WriteLine("Public: " + cls.IsPublic);

                if (cls.Properties.Any())
                {
                    writer.WriteLine("Properties:");
                    foreach (var prop in cls.Properties)
                    {
                        writer.WriteLine("  " + prop);
                    }
                }

                if (cls.Methods.Any())
                {
                    writer.WriteLine("Methods:");
                    foreach (var method in cls.Methods)
                    {
                        writer.WriteLine("  " + method);
                    }
                }

                if (cls.Events.Any())
                {
                    writer.WriteLine("Events:");
                    foreach (var evt in cls.Events)
                    {
                        writer.WriteLine("  " + evt);
                    }
                }

                writer.WriteLine(new string('-', 40));
                writer.WriteLine();
            }
        }
    }

    class ClassInfo
    {
        public string Name { get; set; }
        public string BaseClass { get; set; }
        public bool IsPublic { get; set; }
        public List<string> Properties { get; set; }
        public List<string> Methods { get; set; }
        public List<string> Events { get; set; }
    }
}
