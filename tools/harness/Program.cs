using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

static class Program {
  static int Main(string[] args) {
    string filter = args.Length > 0 ? args[0] : null;
    int passed = 0, failed = 0, ignored = 0;
    var asm = Assembly.GetExecutingAssembly();
    foreach (var type in asm.GetTypes().Where(t => t.Namespace == "libfivesharp.Tests" && t.IsClass).OrderBy(t => t.Name)) {
      var tests = type.GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() != null).ToArray();
      if (tests.Length == 0) continue;
      var setups = type.GetMethods().Where(m => m.GetCustomAttribute<SetUpAttribute>() != null).ToArray();
      Console.WriteLine("== " + type.Name);
      foreach (var test in tests) {
        if (filter != null && !test.Name.Contains(filter)) continue;
        object instance = Activator.CreateInstance(type);
        try {
          foreach (var s in setups) s.Invoke(instance, null);
          test.Invoke(instance, null);
          Console.WriteLine("  PASS " + test.Name);
          passed++;
        } catch (TargetInvocationException tie) when (tie.InnerException is IgnoreException ig) {
          Console.WriteLine("  SKIP " + test.Name + " (" + ig.Message + ")");
          ignored++;
        } catch (TargetInvocationException tie) {
          Console.WriteLine("  FAIL " + test.Name + ": " + tie.InnerException);
          failed++;
        }
      }
    }
    Console.WriteLine($"\n{passed} passed, {failed} failed, {ignored} ignored");
    return failed == 0 ? 0 : 1;
  }
}
