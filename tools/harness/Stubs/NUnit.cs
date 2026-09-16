// Tiny NUnit stand-in used by the harness runner.
using System;

namespace NUnit.Framework {
  [AttributeUsage(AttributeTargets.Method)] public class TestAttribute : Attribute { }
  [AttributeUsage(AttributeTargets.Method)] public class SetUpAttribute : Attribute { }
  public class AssertionException : Exception { public AssertionException(string m) : base(m) { } }
  public class IgnoreException : Exception { public IgnoreException(string m) : base(m) { } }

  public static class Assert {
    static void Fail(string msg, string user) => throw new AssertionException(user != null ? user + " :: " + msg : msg);
    public static void IsTrue(bool c, string msg = null) { if (!c) Fail("expected true", msg); }
    public static void IsFalse(bool c, string msg = null) { if (c) Fail("expected false", msg); }
    public static void IsNull(object o, string msg = null) { if (o != null) Fail("expected null", msg); }
    public static void IsNotNull(object o, string msg = null) { if (o == null) Fail("expected not null", msg); }
    public static void AreSame(object a, object b, string msg = null) { if (!ReferenceEquals(a, b)) Fail("expected same reference", msg); }
    public static void AreEqual(float expected, float actual, float delta, string msg = null) {
      if (float.IsNaN(actual) || Math.Abs(expected - actual) > delta) Fail($"expected {expected} ± {delta} but was {actual}", msg);
    }
    public static void AreEqual(double expected, double actual, double delta, string msg = null) {
      if (double.IsNaN(actual) || Math.Abs(expected - actual) > delta) Fail($"expected {expected} ± {delta} but was {actual}", msg);
    }
    public static void AreEqual(object expected, object actual, string msg = null) {
      if (expected is IConvertible ce && actual is IConvertible ca && expected is not string && actual is not string) {
        if (Convert.ToDecimal(ce) != Convert.ToDecimal(ca)) Fail($"expected {expected} but was {actual}", msg);
        return;
      }
      if (!Equals(expected, actual)) Fail($"expected {expected} but was {actual}", msg);
    }
    public static void Greater(double a, double b, string msg = null) { if (!(a > b)) Fail($"expected {a} > {b}", msg); }
    public static void Less(double a, double b, string msg = null) { if (!(a < b)) Fail($"expected {a} < {b}", msg); }
    public static void Throws<T>(Action a, string msg = null) where T : Exception {
      try { a(); } catch (T) { return; } catch (Exception e) { Fail($"expected {typeof(T).Name} but got {e.GetType().Name}: {e.Message}", msg); }
      Fail($"expected {typeof(T).Name} but nothing was thrown", msg);
    }
    public static void Ignore(string msg) => throw new IgnoreException(msg);
  }

  public static class StringAssert {
    public static void Contains(string expected, string actual, string msg = null) {
      if (actual == null || !actual.Contains(expected)) throw new AssertionException($"expected \"{actual}\" to contain \"{expected}\"" + (msg != null ? " :: " + msg : ""));
    }
  }
}
